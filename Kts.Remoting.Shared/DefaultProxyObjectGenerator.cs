using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonSerializer;

namespace Kts.Remoting.Shared
{
	/// <summary>
	/// Uses Roslyn to compile a proxy object implementing the specified interface.
	/// </summary>
	public class DefaultProxyObjectGenerator : ObjectGeneratorBase, IProxyObjectGenerator
	{
		public T Create<T>(IMessageHandler handler, ICommonSerializer serializer, string serviceName)
			where T: class
		{
			if (!typeof(T).IsInterface)
				throw new ArgumentException("Datatype should be interface: " + typeof(T));

			var className = "ProxyFor" + typeof(T).Name;

			var assemblies = new HashSet<string>();

			foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
				if (!loaded.IsDynamic && loaded.FullName.StartsWith("System"))
					assemblies.Add(loaded.Location);

			var requestTypes = new List<string>();
			var returnTypes = new List<Type>();
			var code = GenerateClassDefinition<T>(className, assemblies, requestTypes, returnTypes);
			var assembly = CompileAndLoadClassDefinition(code, className, assemblies);

			foreach (var rType in returnTypes.Distinct())
			{
				var inh = typeof(ResponseMessage<>).MakeGenericType(rType);
				serializer.RegisterSubtype<Message>(inh, inh.GetHashCode());
			}

			foreach (var rType in assembly.GetTypes().Where(t => requestTypes.Contains(t.Name)))
			{
				var inh = typeof(RequestMessage<>).MakeGenericType(rType);
				serializer.RegisterSubtype<Message>(inh, inh.GetHashCode());
			}

			var type = assembly.GetType(className);
			return (T)Activator.CreateInstance(type, handler, serializer, serviceName);
		}

		internal string GenerateClassDefinition<T>(string className, HashSet<string> assemblies, List<string> addedTypeNames, List<Type> returnTypes)
		{
			var perMethodTypes = new List<string>();
			var sb = new StringBuilder();
			sb.AppendLine("using Kts.Remoting.Shared;");
			sb.Append("public class ");
			sb.Append(className);
			sb.Append(": ");
			sb.Append(FormatType(typeof(ProxyBase), assemblies));
			sb.Append(", ");
			sb.AppendLine(FormatType(typeof(T), assemblies));
			sb.AppendLine("{");

			sb.Append("\tpublic ");
			sb.Append(className);
			sb.Append("(");
			sb.Append(FormatType(typeof(IMessageHandler), assemblies));
			sb.Append(" handler, ");
			sb.Append(FormatType(typeof(ICommonSerializer), assemblies));
			sb.AppendLine(" serializer, string hubName) : base(handler, serializer, hubName) {}");

			var methods = typeof(T).GetMethods();
			foreach (var method in methods)
			{
				if (!typeof(Task).IsAssignableFrom(method.ReturnType))
					throw new NotSupportedException("Expected all methods to return type Task (or Task<>).");

				string typeName;
				perMethodTypes.Add(GenerateMethodTypes(method, assemblies, out typeName));
				addedTypeNames.Add(typeName);

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
				sb.AppendFormat("RequestMessage<{0}>", typeName);
				sb.AppendLine("();");
				sb.Append("\t\tmsg.Method = \"");
				sb.Append(typeName);
				sb.AppendLine("\";");
				sb.Append("\t\tmsg.Arguments = new ");
				sb.Append(typeName);
				sb.AppendLine("();");

				// now copy parameters to instance
				for (int i = 0; i < parameters.Length; i++)
				{
					sb.AppendFormat("\t\tmsg.Arguments.{0} = {0};", parameters[i].Name);
					sb.AppendLine();
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

			foreach (var cls in perMethodTypes)
				sb.AppendLine(cls);

			return sb.ToString();
		}

	}
}
