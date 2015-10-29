using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ProtoBuf.Meta;
using WampSharp.Core.Message;
using WampSharp.Core.Serialization;
using WampSharp.V2.Binding;
using WampSharp.V2.Binding.Parsers;
using WampSharp.V2.Core.Contracts;

namespace Kts.Remoting.Benchmarks
{
	public class ProtobufBinding : WampTransportBinding<ProtobufToken, byte[]>, IWampBinaryBinding<ProtobufToken>
	{
		private static readonly ProtobufFormatter _defaultFormatter = new ProtobufFormatter();

		public ProtobufBinding()
			: base(_defaultFormatter, new ProtobufMessageParser(_defaultFormatter), "wamp.2.protobuf-net")
		{
		}

		public ProtobufBinding(ProtobufFormatter formatter)
			: base(formatter, new ProtobufMessageParser(formatter), "wamp.2.protobuf-net")
		{
		}
	}


	public class ProtobufFormatter : IWampFormatter<ProtobufToken>
	{
		private readonly RuntimeTypeModel _runtime;

		public ProtobufFormatter(MethodInfo classFactory = null)
			: this(TypeModel.Create())
		{
			if (classFactory != null)
				_runtime.SetDefaultFactory(classFactory);
		}

		public ProtobufFormatter(RuntimeTypeModel runtime)
		{
			_runtime = runtime ?? TypeModel.Create();

			_runtime.InferTagFromNameDefault = true; // wouldn't need this if we set the Order property on our DataContract types' members
			_runtime.UseImplicitZeroDefaults = false; // not sure if we need this

			//_runtime.Add(typeof(WampMessage<object>), false).Add("MessageType", "Arguments"); // MessageType is an invalid enum (containing duplicates)
			//_runtime.Add(typeof(WampMessage<byte[]>), false).Add("MessageType", "Arguments");

			var messageSubber = _runtime.Add(typeof(WampDetailsOptions), true);
			var messageTypes = typeof(WampDetailsOptions).Assembly.GetTypes().Where(t => typeof(WampDetailsOptions).IsAssignableFrom(t) && !t.IsAbstract).ToList();
			for (int i = 0; i < messageTypes.Count; i++)
				messageSubber.AddSubType(i + 100, messageTypes[i]);
		}

		public bool CanConvert(ProtobufToken argument, Type type)
		{
			return true;
		}

		public TTarget Deserialize<TTarget>(Stream stream)
		{
			return (TTarget)_runtime.DeserializeWithLengthPrefix(stream, null, typeof(TTarget), ProtoBuf.PrefixStyle.Fixed32, 0);
		}

		public TTarget Deserialize<TTarget>(ProtobufToken message)
		{
			return (TTarget)Deserialize(typeof(TTarget), message);
		}

		public object Deserialize(Type type, ProtobufToken message)
		{
			using (var ms = new MemoryStream(message.Bytes, false))
			{
				return _runtime.DeserializeWithLengthPrefix(ms, null, type, ProtoBuf.PrefixStyle.Fixed32, 0);
			}
		}

		public ProtobufToken Serialize(object value)
		{
			object[] objects = value as object[];

			if (objects != null)
			{
				var arguments = objects.Select(SerializeSingle).ToArray(); // need length on each one
				return SerializeSingle(arguments);
			}

			return SerializeSingle(value);
		}

		private ProtobufToken SerializeSingle(object value)
		{
			using (var ms = new MemoryStream())
			{
				Serialize(value, ms);
				return new ProtobufToken { Bytes = ms.ToArray() };
			}
		}

		public void Serialize(object value, Stream stream)
		{
			_runtime.SerializeWithLengthPrefix(stream, value, value.GetType(), ProtoBuf.PrefixStyle.Fixed32, 0);
		}
	}

	public class ProtobufMessageParser : IWampMessageParser<ProtobufToken, byte[]>
	{
		private readonly ProtobufFormatter _formatter;
		public ProtobufMessageParser(ProtobufFormatter formatter)
		{
			_formatter = formatter;
		}

		public WampMessage<ProtobufToken> Parse(Stream stream)
		{
			// manually serializing this because Protobuf rejects WampMessageType due to duplicate values
			var header = new byte[4];
			for (int i = 0; i < 4; i++)
				header[i] = (byte)stream.ReadByte();
			var messageType = (WampMessageType)BitConverter.ToInt32(header, 0);
			return new WampMessage<ProtobufToken>
			{
				MessageType = messageType,
				Arguments = _formatter.Deserialize<ProtobufToken[]>(stream)
			};
		}

		public void Format(WampMessage<object> message, Stream stream)
		{
			var header = BitConverter.GetBytes((int)message.MessageType);
			stream.Write(header, 0, header.Length);
			var arguments = message.Arguments.Select(a => _formatter.Serialize(a)).ToArray(); // need length on each one
			_formatter.Serialize(arguments, stream);
		}

		public WampMessage<ProtobufToken> Parse(byte[] raw)
		{
			using (var ms = new MemoryStream(raw, false))
				return Parse(ms);
		}

		public byte[] Format(WampMessage<object> message)
		{
			using (var ms = new MemoryStream())
			{
				Format(message, ms);
				return ms.ToArray();
			}
		}
	}

	[ProtoBuf.ProtoContract]
	public struct ProtobufToken
	{
		[ProtoBuf.ProtoMember(1)]
		public byte[] Bytes { get; set; }
	}

}