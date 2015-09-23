using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonSerializer;
using Microsoft.CSharp;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

namespace Kts.Remoting.Client
{
	public class CodeDomProxyClassGenerator : IProxyClassGenerator
	{
		internal string GenerateClassDefinition<T>(string className)
		{
			var sb = new StringBuilder();
			sb.AppendLine("[assembly: System.Runtime.Versioning.TargetFramework(\".NETFramework,Version=v4.5.1\")]");
			sb.Append("public class ");
			sb.Append(className);
			sb.Append(": Kts.Remoting.Client.ProxyBase, ");
			sb.AppendLine(FormatType(typeof(T)));
			sb.AppendLine("{");

			sb.Append("\tpublic ");
			sb.Append(className);
			sb.Append("(");
			sb.Append(FormatType(typeof(ICommonWebSocket)));
			sb.Append(" socket, ");
			sb.Append(FormatType(typeof(ICommonSerializer)));
			sb.AppendLine(" serializer, string hubName) : base(socket, serializer, hubName) {}");

			var methods = typeof(T).GetMethods();
			foreach(var method in methods)
			{
				if (!typeof(Task).IsAssignableFrom(method.ReturnType))
					throw new NotSupportedException("Expected all methods to return type Task (or Task<>).");

				sb.Append("\tpublic ");
				sb.Append(FormatType(method.ReturnType));
				sb.Append(" ");
				sb.Append(method.Name);
				if (method.ContainsGenericParameters)
				{
					sb.Append('<');
					foreach (var generic in method.GetGenericArguments())
					{
						sb.Append(FormatType(generic));
						sb.Append(",");
					}
					sb[sb.Length - 1] = '>';
				}
				sb.Append("(");
				var parameters = method.GetParameters();
				for(int i = 0; i < parameters.Length; i++)
				{
					sb.Append(FormatType(parameters[i].ParameterType));
					sb.Append(" ");
					sb.Append(parameters[i].Name);
					if (i < parameters.Length - 1)
						sb.Append(", ");
				}
				sb.AppendLine(")");
				sb.AppendLine("\t{");
				sb.Append("\t\tvar msg = new ");
				sb.Append(FormatType(typeof(Message)));
				sb.AppendLine("();");
				sb.Append("\t\tmsg.Method = \"");
				sb.Append(method.Name);
				sb.AppendLine("\";");
				sb.AppendLine("\t\tmsg.Arguments = _serializer.GenerateContainer();");

				// now serialize the parameters
				for (int i = 0; i < parameters.Length; i++)
				{
					sb.Append("\t\t_serializer.Serialize(");
					sb.Append(parameters[i].Name);
					sb.AppendLine(", msg.Arguments);");
				}

				sb.Append("\t\treturn Send<");
				if (method.ReturnType == typeof(Task))
					sb.Append("bool");
				else
					sb.Append(FormatType(method.ReturnType.GetGenericArguments().Single()));
				sb.AppendLine(">(msg);");
				sb.AppendLine("\t}");
			}

			sb.AppendLine("}");
			return sb.ToString();
		}

		private string FormatType(Type t)
		{
			return _provider.GetTypeOutput(new CodeTypeReference(t));
		}

		internal void CompileAndLoadClassDefinition<T>(string def)
		{
			using (var ms = new MemoryStream())
			{
				string assemblyFileName = "gen" + Guid.NewGuid().ToString().Replace("-", "") + ".dll";

				CSharpCompilation compilation = CSharpCompilation.Create(assemblyFileName,
					new[] { CSharpSyntaxTree.ParseText(fooSource) },
					new[]
					{
						new MetadataReference(typeof (object).Assembly.Location)
					},
					new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
					);

				compilation.Emit(ms);
				Assembly assembly = Assembly.Load(ms.GetBuffer());
				return assembly;
			}
		}

		public T Create<T>(ICommonWebSocket socket, ICommonSerializer serializer)
			where T : class
		{
			var className = "ProxyFor" + typeof(T).Name;
			var def = GenerateClassDefinition<T>(className);
			CompileAndLoadClassDefinition<T>(def);

			var hubName = typeof(T).Name; // for now
			var type = Type.GetType(className);
			if (type == null)
				throw new Exception("Failed to get type " + className);

			return (T)Activator.CreateInstance(type, socket, serializer, hubName);
		}
	}
}
