using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CSharp;

namespace Kts.Remoting.Shared
{
	public abstract class ObjectGeneratorBase
	{
		private readonly CSharpCodeProvider _provider = new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v4.0" } });
		protected string FormatType(Type t, HashSet<string> assemblies)
		{
			assemblies.Add(t.Assembly.Location);
			return _provider.GetTypeOutput(new CodeTypeReference(t));
		}

		protected string GenerateMethodTypes(MethodInfo method, HashSet<string> assemblies, out string typeName)
		{
			var hash = method.Name.GetHashCode();
			foreach (var parameter in method.GetParameters())
			{
				hash ^= parameter.ParameterType.GetHashCode();
			}

			typeName = method.Name + unchecked((uint)hash);

			var sb = new StringBuilder();
			sb.AppendLine("[" + FormatType(typeof(DataContractAttribute), assemblies) + "]");
			sb.AppendLine("public class " + typeName + " {");
			for (int i = 0; i < method.GetParameters().Length; i++)
			{
				sb.AppendLine("\t[" + FormatType(typeof(DataMemberAttribute), assemblies) + "(Order = " + (i + 1) + ")]");
				var parameter = method.GetParameters()[i];
				sb.Append("\tpublic ");
				sb.Append(FormatType(parameter.ParameterType, assemblies));
				sb.AppendLine(" " + parameter.Name + " {get; set;}");
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
