using System;
using System.IO;
using System.Reflection;
using System.Text;
using ProtoBuf;
using ProtoBuf.Meta;

namespace CommonSerializer.ProtobufNet
{
	public class ProtobufCommonSerializer: ICommonSerializer
	{
		private readonly RuntimeTypeModel _runtime;

		public ProtobufCommonSerializer(MethodInfo classFactory = null)
		{
			_runtime = TypeModel.Create();
			if (classFactory != null)
				_runtime.SetDefaultFactory(classFactory);
		}

		public ProtobufCommonSerializer(RuntimeTypeModel runtime)
		{
			_runtime = runtime;
		}

		public string Description
		{
			get
			{
				return "M. Gravell's Protocol Buffers Implementation";
			}
		}

		public string Name
		{
			get
			{
				return "Protobuf-net";
			}
		}

		public bool StreamsUtf8
		{
			get
			{
				return false;
			}
		}

		public T DeepClone<T>(T t)
		{
			return (T)_runtime.DeepClone(t);
		}

		public object Deserialize(string str, Type type)
		{
			using (var reader = new StringReader(str))
				return Deserialize(reader, type);
		}

		public object Deserialize(TextReader reader, Type type)
		{
			var line = reader.ReadLine();
			if (line == null)
				return null;
			var bytes = Convert.FromBase64String(line);
			using (var ms = new MemoryStream(bytes, false))
				return Deserialize(ms, type);
		}

		public object Deserialize(Stream stream, Type type)
		{
			return _runtime.DeserializeWithLengthPrefix(stream, null, type, PrefixStyle.Fixed32, 0);
		}

		public object Deserialize(ISerializedContainer container, Type type)
		{
			var psc = container as ProtobufSerializedContainer;
			if (psc == null)
				throw new ArgumentException("Invalid container type. Use the GenerateContainer method.");

			return Deserialize(psc.Stream, type);
		}

		public T Deserialize<T>(Stream stream)
		{
			return (T)Deserialize(stream, typeof(T));
		}

		public T Deserialize<T>(string str)
		{
			return (T)Deserialize(str, typeof(T));
		}

		public T Deserialize<T>(TextReader reader)
		{
			return (T)Deserialize(reader, typeof(T));
		}

		public T Deserialize<T>(ISerializedContainer container)
		{
			return (T)Deserialize(container, typeof(T));
		}

		public ISerializedContainer GenerateContainer()
		{
			return new ProtobufSerializedContainer();
		}

		public void RegisterSubtype<TBase, TInheritor>(int fieldNumber)
		{
			_runtime[typeof(TBase)].AddSubType(fieldNumber, typeof(TInheritor));
		}

		public string Serialize<T>(T t)
		{
			var sb = new StringBuilder();
			using (var stringWriter = new StringWriter(sb))
				Serialize<T>(t, stringWriter);
			return sb.ToString();
		}

		public void Serialize<T>(T t, TextWriter writer)
		{
			using (var stream = new MemoryStream())
			{
				Serialize<T>(t, stream);
				stream.Flush();
				var base64 = Convert.ToBase64String(stream.ToArray());
				writer.Write(base64);
			}
		}

		public void Serialize<T>(T t, Stream stream)
		{
			_runtime.SerializeWithLengthPrefix(stream, t, typeof(T), PrefixStyle.Fixed32, 0);
		}

		public void Serialize<T>(T t, ISerializedContainer container)
		{
			var psc = container as ProtobufSerializedContainer;
			if (psc == null)
				throw new ArgumentException("Invalid container type. Use the GenerateContainer method.");

			Serialize<T>(t, psc.Stream);
			psc.Count++;
		}
	}
}
