using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonSerializer;

namespace Kts.Remoting.Shared
{
	/// <summary>
	/// This uses Roslyn to create a wrapper around the service that parses the method parameters out of the message.
	/// </summary>
	public class DefaultServiceWrapperGenerator : ObjectGeneratorBase, IServiceWrapperGenerator
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

			var requestTypes = new List<string>();
			var returnTypes = new List<Type>();
			var code = GenerateClassDefinition(className, assemblies, service, requestTypes, returnTypes);
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
			return (IMessageHandler)Activator.CreateInstance(type, handler, serializer, service);
		}

		internal string GenerateClassDefinition(string className, HashSet<string> assemblies, object service, List<string> addedTypeNames, List<Type> returnTypes)
		{
			var perMethodTypes = new List<string>();
			var sb = new StringBuilder();
			sb.AppendLine("using Kts.Remoting.Shared;");
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
			sb.AppendLine(" Handle(System.Func<System.Type, Message> getOrCreateMessage)");
			sb.AppendLine("\t{");
			sb.AppendLine("\t\tvar message = getOrCreateMessage.Invoke(null);");
			sb.AppendLine("\t\tswitch(message.Method) {");

			var methods = service.GetType().GetMethods().Where(m => m.IsPublic);
			
			// loop through each method
			// add a case for each
			// for each parameter on each method
			// decode the value and put it into a variable
			// finally call the method with all the parameters
			// awaiting it if it returns a task

			foreach (var method in methods)
			{
				if (method.DeclaringType == typeof(object))
					continue;

				string typeName;
				perMethodTypes.Add(GenerateMethodTypes(method, assemblies, out typeName));
				addedTypeNames.Add(typeName);

				sb.AppendFormat("\t\tcase \"{0}\": {{", typeName);
				sb.AppendLine();

				sb.AppendFormat("\t\t\tdynamic messageA = getOrCreateMessage.Invoke(typeof(RequestMessage<{0}>));", typeName);
				sb.AppendLine();

				// TODO: handle generic methods, handle "out" and "ref" variables
				// TODO: catch exceptions on the method and set the error string

				bool hasResult = false, hasAwait = false;
				if (typeof(Task) == method.ReturnType)
				{
					sb.Append("\t\t\tawait ");
					hasAwait = true;
				}
				else if (typeof(Task).IsAssignableFrom(method.ReturnType))
				{
					sb.Append("\t\t\tvar result = await ");
					hasResult = true;
					hasAwait = true;
				}
				else if (typeof(void) != method.ReturnType)
				{
					sb.Append("\t\t\tvar result = ");
					hasResult = true;
				}
				sb.AppendFormat("_service.{0}(", method.Name);
				foreach(var parameter in method.GetParameters())
				{
					sb.Append("messageA.Arguments.");
					sb.Append(parameter.Name);
					sb.Append(", ");
				}
				sb.Remove(sb.Length - 2, 2);
				sb.Append(")");
				if (hasAwait)
					sb.Append(".ConfigureAwait(false)");
				sb.AppendLine(";");

				sb.Append("\t\t\tvar msg = new ");
				if (hasResult)
				{
					var rType = StripTask(method.ReturnType);
					returnTypes.Add(rType);
					sb.AppendFormat("ResponseMessage<{0}>", FormatType(rType, assemblies));
				}
				else
					sb.Append("ResponseMessage<bool>");

				sb.AppendLine("();");
				sb.AppendLine("\t\t\tmsg.ID = messageA.ID;");
				sb.AppendLine("\t\t\tmsg.Hub = messageA.Hub;");
				sb.AppendLine("\t\t\tmsg.Method = messageA.Method;");
				sb.AppendLine("\t\t\tmsg.SessionID = messageA.SessionID;");

				if (hasResult)
					sb.Append("\t\t\tmsg.Results = result;");
				else
					sb.Append("\t\t\tmsg.Results = true;");
				sb.AppendLine();
				sb.AppendLine("\t\t\tawait _handler.Handle(type => msg).ConfigureAwait(false);");
				sb.AppendLine("\t\t\t} break;");
			}

			sb.AppendLine("\t\tdefault:");
			sb.AppendLine("\t\t\tthrow new System.Exception(\"Unable to handle method.\");");
			// TODO: add default case that returns method with error set

			sb.AppendLine("\t\t}");
			sb.AppendLine("\t}");
			sb.AppendLine("}");

			foreach (var cls in perMethodTypes)
				sb.AppendLine(cls);

			return sb.ToString();
		}

		private Type StripTask(Type returnType)
		{
			if (typeof(Task).IsAssignableFrom(returnType))
				return returnType.GetGenericArguments().Single();
			return returnType;
		}
	}
}
