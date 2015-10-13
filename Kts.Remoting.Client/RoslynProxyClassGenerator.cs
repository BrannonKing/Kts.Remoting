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

namespace Kts.Remoting.Client
{
	public class RoslynProxyClassGenerator : IProxyClassGenerator
	{
		internal string GenerateClassDefinition<T>(string className, HashSet<string> assemblies)
		{
			var sb = new StringBuilder();
			sb.Append("public class ");
			sb.Append(className);
			sb.Append(": Kts.Remoting.Client.ProxyBase, ");
			sb.AppendLine(FormatType(typeof(T), assemblies));
			sb.AppendLine("{");

			sb.Append("\tpublic ");
			sb.Append(className);
			sb.Append("(");
			sb.Append(FormatType(typeof(ICommonTransport), assemblies));
			sb.Append(" socket, ");
			sb.Append(FormatType(typeof(ICommonSerializer), assemblies));
			sb.AppendLine(" serializer, string hubName) : base(socket, serializer, hubName) {}");

			var methods = typeof(T).GetMethods();
			foreach (var method in methods)
			{
				if (!typeof(Task).IsAssignableFrom(method.ReturnType))
					throw new NotSupportedException("Expected all methods to return type Task (or Task<>).");

				sb.Append("\tpublic ");
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

		private readonly CSharpCodeProvider _provider = new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v4.0" } });
		private string FormatType(Type t, HashSet<string> assemblies)
		{
			assemblies.Add(t.Assembly.Location);
			return _provider.GetTypeOutput(new CodeTypeReference(t));
		}

		internal Assembly CompileAndLoadClassDefinition<T>(string code, string className, HashSet<string> assemblies)
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

		public T Create<T>(ICommonTransport socket, ICommonSerializer serializer, string hubName = null)
			where T : class
		{
			if (!typeof(T).IsInterface)
				throw new ArgumentException("Datatype should be interface: " + typeof(T));

			var className = "ProxyFor" + typeof(T).Name;

			var assemblies = new HashSet<string>
			{
				typeof(ProxyBase).Assembly.Location,
				typeof(T).Assembly.Location,
			};

			foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
				if (!loaded.IsDynamic && loaded.FullName.StartsWith("System"))
					assemblies.Add(loaded.Location);

			var code = GenerateClassDefinition<T>(className, assemblies);
			var assembly = CompileAndLoadClassDefinition<T>(code, className, assemblies);

			if (hubName == null)
				hubName = typeof(T).Name;
			var type = assembly.GetType(className);
			return (T)Activator.CreateInstance(type, socket, serializer, hubName);
		}
	}
}
