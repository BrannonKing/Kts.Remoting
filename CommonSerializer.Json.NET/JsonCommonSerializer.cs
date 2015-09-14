using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CommonSerializer.Json.NET
{
	// This project can output the Class library as a NuGet Package.
	// To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
	public class JsonCommonSerializer : ICommonSerializer
	{
		private readonly JsonSerializer _serializer;

		public JsonCommonSerializer(bool indented = false)
			: this(new JsonSerializer
				{
					TypeNameHandling = TypeNameHandling.Auto,
					Formatting = indented ? Formatting.Indented : Formatting.None,
				})
		{
		}

		public JsonCommonSerializer(JsonSerializer serializer)
		{
			_serializer = serializer;
		}

		public string Description
		{
			get
			{
				return "Newtonsoft Json.NET";
			}
		}

		public string Name
		{
			get
			{
				return "Json.NET";
			}
		}

		public bool StreamsUtf8
		{
			get
			{
				return true;
			}
		}

		public T DeepClone<T>(T t)
		{
			using (var ms = new MemoryStream())
			{
				Serialize(t, ms);
				ms.Position = 0;
				return (T)Deserialize(ms, t.GetType());

				// alternate that will use less memory and might be faster (not tested yet):
				//using (var writer = new BsonWriter(ms))
				//	_serializer.Serialize(writer, t);
				//ms.Position = 0;
				//using (var reader = new BsonReader(ms))
				//	return _serializer.Deserialize<T>(reader);
			}
		}

		public object Deserialize(string str, Type type)
		{
			using (var reader = new StringReader(str))
				return Deserialize(reader, type);
		}

		public object Deserialize(TextReader reader, Type type)
		{
			using (var jsonReader = new JsonTextReader(reader) { CloseInput = false })
				return _serializer.Deserialize(jsonReader, type);
		}

		public object Deserialize(Stream stream, Type type)
		{
			using (var utfReader = new StreamReader(stream, Encoding.UTF8, true, 2048, true))
			using (var reader = new JsonTextReader(utfReader) { CloseInput = false })
				return _serializer.Deserialize(reader, type);
		}

		public object Deserialize(ISerializedContainer container, Type type)
		{
			var jTokenContainer = container as JTokenContainer;
			if (jTokenContainer == null)
				throw new ArgumentException("Invalid container. Use the GenerateContainer method.");

			if (jTokenContainer.HasValues)
			{
				using (var reader = new JTokenReader(jTokenContainer))
					return _serializer.Deserialize(reader, type);
			}
			return null;
		}

		public T Deserialize<T>(TextReader reader)
		{
			return (T)Deserialize(reader, typeof(T));
		}

		public T Deserialize<T>(string str)
		{
			return (T)Deserialize(str, typeof(T));
		}

		public T Deserialize<T>(Stream stream)
		{
			return (T)Deserialize(stream, typeof(T));
		}

		public T Deserialize<T>(ISerializedContainer container)
		{
			return (T)Deserialize(container, typeof(T));
		}

		private class JTokenContainer : JObject, ISerializedContainer
		{
		}

		public ISerializedContainer GenerateContainer()
		{
			return new JTokenContainer();
		}

		public string Serialize<T>(T t)
		{
			var sb = new StringBuilder();
			using (var stringWriter = new StringWriter(sb))
			using (var writer = new JsonTextWriter(stringWriter) { CloseOutput = false })
				_serializer.Serialize(writer, t, typeof(T));

			return sb.ToString();
		}

		public void Serialize<T>(T t, TextWriter writer)
		{
			using (var jsonWriter = new JsonTextWriter(writer) { CloseOutput = false })
				_serializer.Serialize(jsonWriter, t, typeof(T));
		}

		public void Serialize<T>(T t, Stream stream)
		{
			using (var utfWriter = new StreamWriter(stream, Encoding.UTF8, 2048, true))
			using (var jsonWriter = new JsonTextWriter(utfWriter) { CloseOutput = false })
				_serializer.Serialize(jsonWriter, t, typeof(T));
		}

		public void Serialize<T>(T t, ISerializedContainer container)
		{
			var jTokenContainer = container as JTokenContainer;
			if (jTokenContainer == null)
				throw new ArgumentException("Invalid container. Use the GenerateContainer method.");

			using (var writer = new JTokenWriter(jTokenContainer))
				_serializer.Serialize(writer, t, typeof(T));
		}
	}
}
