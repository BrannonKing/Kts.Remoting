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
	public class RoslynServiceWrapperGenerator : IServiceWrapperGenerator
	{
		public IDisposable Create(ICommonTransport transport, ICommonSerializer serializer, object service, string serviceName = null)
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
			return (IDisposable)Activator.CreateInstance(type, transport, serializer);
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
			sb.AppendLine(className);
			sb.AppendLine("{");

			sb.Append("\tpublic ");
			sb.Append(className);
			sb.Append("(");
			sb.Append(FormatType(typeof(ICommonTransport), assemblies));
			sb.Append(" socket, ");
			sb.Append(FormatType(typeof(ICommonSerializer), assemblies));
			sb.AppendLine(" serializer, string hubName) : base(socket, serializer) {}");

				sb.Append("\tpublic ");
			left off: we need some way to route the messages to the correct hub
				sb.Append(FormatType(method.ReturnType, assemblies));
				sb.Append(" ");


			var methods = service.GetType().GetMethods().Where(m => m.IsPublic);
			foreach (var method in methods)
			{
				// we need to only deserialize that message once
				// add the method name to the switch

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
