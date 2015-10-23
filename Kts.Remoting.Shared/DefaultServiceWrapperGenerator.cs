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

namespace Kts.Remoting.Shared
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
			return (IMessageHandler)Activator.CreateInstance(type, handler, serializer, service);
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

			sb.Append("\tprivate readonly ");
			sb.Append(FormatType(typeof(IMessageHandler), assemblies));
			sb.AppendLine(" _handler;");

			sb.Append("\tprivate readonly ");
			sb.Append(FormatType(typeof(ICommonSerializer), assemblies));
			sb.AppendLine(" _serializer;");

			sb.Append("\tprivate readonly ");
			sb.Append(FormatType(service.GetType(), assemblies));
			sb.AppendLine(" _service;");

			sb.Append("\tpublic ");
			sb.Append(className);
			sb.Append("(");
			sb.Append(FormatType(typeof(IMessageHandler), assemblies));
			sb.Append(" handler, ");
			sb.Append(FormatType(typeof(ICommonSerializer), assemblies));
			sb.Append(" serializer, ");
			sb.Append(FormatType(service.GetType(), assemblies));
			sb.AppendLine(" service) { _handler = handler; _serializer = serializer; _service = service; }");

			sb.Append("\tpublic async ");
			sb.Append(FormatType(typeof(Task), assemblies));
			sb.Append(" Handle(");
			sb.Append(FormatType(typeof(Message), assemblies));
			sb.AppendLine(" message)");
			sb.AppendLine("\t{");
			sb.AppendLine("\t\tswitch(message.Method) {");

			var methods = service.GetType().GetMethods().Where(m => m.IsPublic);
			
			var names = methods.Select(m => m.Name).ToList();
			var distinct = names.Distinct().ToList();
			if (names.Count != distinct.Count)
				throw new ArgumentException("Method overloads are not supported (yet).");

			// loop through each method
			// add a case for each
			// for each parameter on each method
			// decode the value and put it into a variable
			// finally call the method with all the parameters
			// awaiting it if it returns a task

			int variable = 0;
			foreach (var method in methods)
			{
				if (method.DeclaringType == typeof(object))
					continue;

				sb.Append("\t\tcase \"");
				sb.Append(method.Name);
				sb.AppendLine("\":");

				var startVar = variable;

				// TODO: handle generic methods, handle "out" and "ref" variables
				// TODO: catch exceptions on the method and set the error string
				var parameters = method.GetParameters();
				foreach (var parameter in parameters)
				{
					sb.Append("\t\t\tvar var");
					sb.Append(variable++);
					sb.Append(" = _serializer.Deserialize<");
					sb.Append(FormatType(parameter.ParameterType, assemblies));
					sb.AppendLine(">(message.Arguments);");
				}

				bool hasResult = false;
				if (typeof(Task) == method.ReturnType)
					sb.Append("\t\t\tawait ");
				else if (typeof(Task).IsAssignableFrom(method.ReturnType))
				{
					sb.Append("\t\t\tvar result = await ");
					hasResult = true;
				}
				else if (typeof(void) != method.ReturnType)
				{
					sb.Append("\t\t\tvar result = ");
					hasResult = true;
				}
				sb.Append("_service.");
				sb.Append(method.Name);
				sb.Append("(");
				while(startVar < variable)
				{
					sb.Append("var");
					sb.Append(startVar++);
					if (startVar < variable)
						sb.Append(", ");
				}
				sb.AppendLine(");");
				sb.AppendLine("\t\t\tmessage.Arguments = null;");
				sb.AppendLine("\t\t\tmessage.Results = _serializer.GenerateContainer();");
				if (hasResult)
				{
					sb.AppendLine("\t\t\t_serializer.Serialize(message.Results, result);");
				}
				sb.AppendLine("\t\t\tawait _handler.Handle(message);");
				sb.AppendLine("\t\t\tbreak;");
			}

			sb.AppendLine("\t\t}");
			sb.AppendLine("\t}");
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
