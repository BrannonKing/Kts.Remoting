﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonSerializer;

namespace Kts.Remoting.Client
{
	public class CodeDomProxyClassGenerator : IProxyClassGenerator
	{
		internal string GenerateClassDefinition<T>(string className)
		{
			var sb = new StringBuilder();
			sb.Append("class ");
			sb.Append(className);
			sb.Append(": Kts.Remoting.Client.ProxyBase, ");
			sb.AppendLine(typeof(T).FullName);
			sb.AppendLine("{");

			sb.Append("\tpublic ");
			sb.Append(className);
			sb.Append("(");
			sb.Append(typeof(ICommonWebSocket).FullName);
			sb.Append(" socket, ");
			sb.Append(typeof(ICommonSerializer).FullName);
			sb.AppendLine(" serializer, string hubName) : base(socket, serializer, hubName) {}");

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
					sb.Append(method.ReturnType.GetGenericArguments().Single().FullName);
				sb.AppendLine(">(msg);");
				sb.AppendLine("\t}");
			}

			sb.AppendLine("}");
			return sb.ToString();
		}

		internal void CompileAndLoadClassDefinition<T>(string def)
		{
			var provider = CodeDomProvider.CreateProvider("CSharp");
			var cp = new CompilerParameters();

			// Generate an executable instead of  
			// a class library.
			cp.GenerateExecutable = true;

			// Set the assembly file name to generate.
			//cp.OutputAssembly = newExeFilename;

			// Generate debug information.
			cp.IncludeDebugInformation = false;

			// Add an assembly reference.
			var references = new HashSet<string>{"System"};
			foreach (var type in typeof(T).GetInterfaces())
				references.Add(type.Assembly.GetName().Name);
			foreach (var method in typeof(T).GetMethods())
			{
				foreach (var type in method.GetGenericArguments())
					references.Add(type.Assembly.GetName().Name);
				foreach (var type in method.GetParameters().Select(p => p.ParameterType))
					references.Add(type.Assembly.GetName().Name);
				references.Add(method.ReturnType.Assembly.GetName().Name);
			}

			foreach (var assembly in references)
				cp.ReferencedAssemblies.Add(assembly);
			
			// Save the assembly as a physical file.
			cp.GenerateInMemory = true; // even when true it still writes a file; we just hope it removes the file as well

			// Set the level at which the compiler  
			// should start displaying warnings.
			cp.WarningLevel = 3;

			// Set whether to treat all warnings as errors.
			cp.TreatWarningsAsErrors = false;

			// Set compiler argument to optimize output.
			cp.CompilerOptions = "/optimize";

			// Set a temporary files collection. 
			// The TempFileCollection stores the temporary files 
			// generated during a build in the current directory, 
			// and does not delete them after compilation.
			cp.TempFiles = new TempFileCollection(Path.GetTempPath(), false);

			// Specify the class that contains  
			// the main method of the executable.
			//cp.MainClass = "Generator.Program";

			// Set the embedded resource file of the assembly. 
			//cp.EmbeddedResources.Add(resourceFilename);

			//var attributes = Assembly.GetExecutingAssembly().CustomAttributes;
			//var fileVersion = attributes.Single(a => a.AttributeType == typeof(AssemblyFileVersionAttribute));
			//var prodVersion = attributes.SingleOrDefault(a => a.AttributeType == typeof(AssemblyVersionAttribute)) ?? fileVersion;
			//var aa = "[assembly: System.Reflection.AssemblyFileVersionAttribute(" + fileVersion.ConstructorArguments[0] + ")]" + Environment.NewLine;
			//aa += "[assembly: System.Reflection.AssemblyVersionAttribute(" + prodVersion.ConstructorArguments[0] + ")]" + Environment.NewLine;
			
			var results = provider.CompileAssemblyFromSource(cp, def);

			if (results.Errors.HasErrors)
				throw new Exception("Unable to compile installer package. Message: " + string.Join(". ", results.Errors));
		}

		public T Create<T>(ICommonWebSocket socket, ICommonSerializer serializer)
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