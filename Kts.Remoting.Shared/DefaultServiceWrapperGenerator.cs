using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CommonSerializer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;

namespace Kts.Remoting
{
	/// <summary>
	/// This uses Roslyn to create a wrapper around the service that parses the method parameters out of the message.
	/// </summary>
	public class DefaultServiceWrapperGenerator : IServiceWrapperGenerator
	{
		public IMessageHandler Create(IMessageHandler handler, ICommonSerializer serializer, object service)
		{
			// subscribe to events on the service and forward them to the transport
			// generate a method that can handle incoming messages
			// it needs to send the results to the caller
			// do we need to tell the servive about the caller? no. nice but not required. we can still call a method to get current state

			var className = "WrapperFor" + service.GetType().Name;

			var assemblies = new HashSet<string>();

			foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
				if (!loaded.IsDynamic && loaded.FullName.StartsWith("System"))
					assemblies.Add(loaded.Location);

			var code = GenerateClassDefinition(className, assemblies, service);
			var assembly = CompileAndLoadClassDefinition(code, className, assemblies);

			var type = assembly.GetType(className);
			return (IMessageHandler)Activator.CreateInstance(type, handler, serializer);
		}

		private readonly CSharpCodeProvider _provider = new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v4.0" } });
		private string FormatType(Type t, HashSet<string> assemblies)
		{
			assemblies.Add(t.Assembly.Location);
			return _provider.GetTypeOutput(new CodeTypeReference(t));
		}

		internal string GenerateClassDefinition(string className, HashSet<string> assemblies, object service)
		{
			var sb = new StringBuilder();
			sb.Append("public class ");
			sb.Append(className);
			sb.Append(": ");
			sb.AppendLine(FormatType(typeof(IMessageHandler), assemblies));
			sb.AppendLine("{");

			sb.Append("\tpublic ");
			sb.Append(className);
			sb.Append("(");
			sb.Append(FormatType(typeof(IMessageHandler), assemblies));
			sb.Append(" handler, ");
			sb.Append(FormatType(typeof(ICommonSerializer), assemblies));
			sb.AppendLine(" serializer) { _handler = handler; _serializer = serializer; }");

			sb.Append("\tpublic async ");
			sb.Append(FormatType(typeof(Task), assemblies));
			sb.Append(" Send(");
			sb.Append(FormatType(typeof(Message), assemblies));
			sb.AppendLine(" message)");
			sb.AppendLine("\t{");
			sb.AppendLine("\tswitch(message.Method) {");

			var methods = service.GetType().GetMethods().Where(m => m.IsPublic);
			
			var names = methods.Select(m => m.Name).ToList();
			var distinct = names.Distinct().ToList();
			if (names.Count != distinct.Count)
				throw new ArgumentException("Method overloads are not supported (yet).");

			foreach (var method in methods)
			{
				// await it if possible
				sb.Append("\t\tcase ");
				sb.Append(method.Name);
				sb.AppendLine(":");


				left off:

				sb.Append(FormatType(method.ReturnType, assemblies));
				sb.Append(" ");

				sb.Append(method.Name);
				if (method.ContainsGenericParameters)
				{
					sb.Append('<');
					foreach (var generic in method.GetGenericArguments())
					{
						sb.Append(FormatType(generic, assemblies));
						sb.Append(",");
					}
					sb[sb.Length - 1] = '>';
				}
				sb.Append("(");
				var parameters = method.GetParameters();
				for (int i = 0; i < parameters.Length; i++)
				{
					sb.Append(FormatType(parameters[i].ParameterType, assemblies));
					sb.Append(" ");
					sb.Append(parameters[i].Name);
					if (i < parameters.Length - 1)
						sb.Append(", ");
				}
				sb.AppendLine(")");
				sb.AppendLine("\t{");
				sb.Append("\t\tvar msg = new ");
				sb.Append(FormatType(typeof(Message), assemblies));
				sb.AppendLine("();");
				sb.Append("\t\tmsg.Method = \"");
				sb.Append(method.Name);
				sb.AppendLine("\";");
				sb.AppendLine("\t\tmsg.Arguments = _serializer.GenerateContainer();");

				// now serialize the parameters
				for (int i = 0; i < parameters.Length; i++)
				{
					sb.Append("\t\t_serializer.Serialize(msg.Arguments, ");
					sb.Append(parameters[i].Name);
					sb.AppendLine(");");
				}

				sb.Append("\t\treturn Send<");
				if (method.ReturnType == typeof(Task))
					sb.Append("bool");
				else
					sb.Append(FormatType(method.ReturnType.GetGenericArguments().Single(), assemblies));
				sb.AppendLine(">(msg);");
				sb.AppendLine("\t}");
			}

			sb.AppendLine("}");
			return sb.ToString();
		}

		internal Assembly CompileAndLoadClassDefinition(string code, string className, HashSet<string> assemblies)
		{
			using (var ms = new MemoryStream())
			{
				string assemblyFileName = className + Guid.NewGuid().ToString().Replace("-", "") + ".dll";


				var references = assemblies.Select(a => MetadataReference.CreateFromFile(a));
				var compilation = CSharpCompilation.Create(assemblyFileName,
					new[] { CSharpSyntaxTree.ParseText(code) }, references,
					new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary
#if !DEBUG
						, optimizationLevel: OptimizationLevel.Release
#endif
						));

				var result = compilation.Emit(ms);
				if (!result.Success)
				{
					var errors = string.Join(Environment.NewLine, result.Diagnostics);
					throw new Exception("Unable to compile. Errors:" + Environment.NewLine + errors);
				}

				var assembly = Assembly.Load(ms.GetBuffer());
				return assembly;
			}
		}
	}
}
