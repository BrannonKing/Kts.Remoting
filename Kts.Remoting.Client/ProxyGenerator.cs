using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kts.Remoting.Client
{
	public class ProxyGenerator
	{
		internal string GenerateClassDefinition<T>()
		{
			var className = "ProxyFor" + typeof(T).Name;
			var hubName = typeof(T).Name; // for now

			var sb = new StringBuilder();
			sb.Append("class ");
			sb.Append(className);
			sb.Append(": Kts.Remoting.Client.ProxyBase, ");
			sb.AppendLine(typeof(T).FullName);
			sb.AppendLine("{");

			var methods = typeof(T).GetMethods();
			foreach(var method in methods)
			{
				if (!typeof(Task).IsAssignableFrom(method.ReturnType))
					throw new NotSupportedException("Expected all methods to return type Task (or Task<>).");

				sb.Append("\tpublic ");
				sb.Append(method.ReturnType.FullName);
				sb.Append(" ");
				sb.Append(method.Name);
				if (method.ContainsGenericParameters)
				{
					sb.Append('<');
					foreach (var generic in method.GetGenericArguments())
					{
						sb.Append(generic.FullName);
						sb.Append(",");
					}
					sb[sb.Length - 1] = '>';
				}
				sb.Append("(");
				var parameters = method.GetParameters();
                for(int i = 0; i < parameters.Length; i++)
				{
					sb.Append(parameters[i].ParameterType.FullName);
					sb.Append(" ");
					sb.Append(parameters[i].Name);
					if (i < parameters.Length - 1)
						sb.Append(", ");
				}
				sb.AppendLine(")");
				sb.AppendLine("\t{");
				sb.Append("\t\tvar msg = new ");
				sb.Append(typeof(Message).FullName);
				sb.AppendLine("();");
				sb.Append("msg.Hub = ");
				sb.Append(hubName);
				sb.AppendLine(";");
				sb.a

			}

			sb.AppendLine("}");
			return sb.ToString();
		}

		internal void CompileAndLoadClassDefinition<T>(string def)
		{

		}

		public T Create<T>()
		{
			var def = GenerateClassDefinition<T>();
			CompileAndLoadClassDefinition<T>(def);

			return (T)Activator.CreateInstance(socket);
		}
	}
}
