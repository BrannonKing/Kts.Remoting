using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Newtonsoft.Json;
using ProtoBuf.Meta;
using WampSharp.Binding;
using WampSharp.Core.Message;
using WampSharp.Core.Serialization;
using WampSharp.Vtortola;
using WampSharp.V2;
using WampSharp.V2.Binding;
using WampSharp.V2.Binding.Parsers;
using WampSharp.V2.Client;
using WampSharp.V2.Core.Contracts;
using WampSharp.V2.Fluent;
using WampSharp.WebSocket4Net;
using Xunit;
using Xunit.Abstractions;

namespace Kts.Remoting.Benchmarks
{
	public class WampSharp
	{
		private readonly ITestOutputHelper _testOutputHelper;

		public WampSharp(ITestOutputHelper testOutputHelper)
		{
			_testOutputHelper = testOutputHelper;
		}

		[Fact]
		public void BenchmarkMessages()
		{

			var host = StartServer();
			var factory = new WampChannelFactory();

			var channel = factory.ConnectToRealm("realm1")
				.WebSocketTransport("ws://127.0.0.1:8080/")
				//.MsgpackSerialization(new JsonSerializer { TypeNameHandling = TypeNameHandling.Auto })
				.JsonSerialization(new JsonSerializer { TypeNameHandling = TypeNameHandling.Auto })
				//.CraAuthentication(authenticationId: "peter", secret: "secret1")
				.Build();

			channel.RealmProxy.Monitor.ConnectionEstablished += (sender, eventArgs) => _testOutputHelper.WriteLine("Connected with ID " + eventArgs.SessionId);

			channel.Open().Wait();
				
			var proxy = channel.RealmProxy.Services.GetCalleeProxy<ISumService>(new CallerNameInterceptor());

			const int randCnt = 100;
			var rand = new Random(42);
			var randoms = new int[randCnt];
			for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);
			var package = new SumPackage { Numbers = randoms };

			var sw = new Stopwatch();
			for (int j = 0; j < 500; j++)
			{
				sw.Start();
				var sum = proxy.SumPackage(package).Result;
				sw.Stop();
				Assert.Equal(randoms.Sum(), sum);
				for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);
			}

			_testOutputHelper.WriteLine("Completed 500 sum passes in {0}ms", sw.Elapsed.TotalMilliseconds);

			sw.Reset();
			var tree = new SumServiceTree();
			SumServiceTree.FillTree(tree, rand, 2);
			_testOutputHelper.WriteLine("Starting large message transfer.");
			sw.Start();
			var result = proxy.Increment(tree).Result;
			sw.Stop();
			Assert.Equal(tree.Leaf + 1, result.Leaf);
			_testOutputHelper.WriteLine("Completed large transfer in {0}ms", sw.Elapsed.TotalMilliseconds);

			channel.Close();
			host.Dispose();
		}

		[Fact]
		public void BenchmarkMessagesProtobuf()
		{
			var host = StartServer();
			var factory = new WampChannelFactory();
			var binding = new ProtobufBinding();
			var connection = new WebSocket4NetBinaryConnection<ProtobufToken>("ws://localhost:8080/", binding);
			var channel = factory.CreateChannel("realm1", connection, binding);

			channel.RealmProxy.Monitor.ConnectionEstablished += (sender, eventArgs) => _testOutputHelper.WriteLine("Connected with ID " + eventArgs.SessionId);

			channel.Open().Wait();

			var proxy = channel.RealmProxy.Services.GetCalleeProxy<ISumService>(new CallerNameInterceptor());

			const int randCnt = 100;
			var rand = new Random(42);
			var randoms = new int[randCnt];
			for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);
			var package = new SumPackage { Numbers = randoms };

			var sw = new Stopwatch();
			for (int j = 0; j < 500; j++)
			{
				sw.Start();
				var sum = proxy.SumPackage(package).Result;
				sw.Stop();
				Assert.Equal(randoms.Sum(), sum);
				for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);
			}

			_testOutputHelper.WriteLine("Completed 500 sum passes in {0}ms", sw.Elapsed.TotalMilliseconds);

			sw.Reset();
			var tree = new SumServiceTree();
			SumServiceTree.FillTree(tree, rand, 2);
			_testOutputHelper.WriteLine("Starting large message transfer.");
			sw.Start();
			var result = proxy.Increment(tree).Result;
			sw.Stop();
			Assert.Equal(tree.Leaf + 1, result.Leaf);
			_testOutputHelper.WriteLine("Completed large transfer in {0}ms", sw.Elapsed.TotalMilliseconds);

			channel.Close();
			host.Dispose();
		}


		public class CallerNameInterceptor : ICalleeProxyInterceptor
		{
			private readonly CallOptions _options = new CallOptions();
			public CallOptions GetCallOptions(MethodInfo method)
			{
				return _options;
			}

			public string GetProcedureUri(MethodInfo method)
			{
				return method.Name;
			}
		}

		private static IDisposable StartServer()
		{
			var host = new WampHost();
			host.RegisterTransport(new VtortolaWebSocketTransport(new IPEndPoint(IPAddress.Loopback, 8080), true),
				new JTokenJsonBinding(), new JTokenMsgpackBinding(), new ProtobufBinding());

			host.Open();

			var realm = host.RealmContainer.GetRealmByName("realm1");
			realm.Services.RegisterCallee(new SumService(), new CalleeNameInterceptor()).Wait(); // add services (aka, RPC endpoints) like this
			// realm.Services.RegisterPublisher // register some event triggerer here

			return host;
		}

		private class CalleeNameInterceptor : ICalleeRegistrationInterceptor
		{
			public bool IsCalleeProcedure(MethodInfo method)
			{
				return method.DeclaringType != typeof(object) && !method.DeclaringType.IsInterface;
			}

			private readonly RegisterOptions _options = new RegisterOptions();
			public RegisterOptions GetRegisterOptions(MethodInfo method)
			{
				return _options;
			}

			public string GetProcedureUri(MethodInfo method)
			{
				return method.Name;
			}
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

			_runtime.InferTagFromNameDefault = true;
			_runtime.UseImplicitZeroDefaults = false;

			//_runtime.Add(typeof(WampMessage<object>), false).Add("MessageType", "Arguments"); // MessageType is an invalid enum (containing duplicates)
			//_runtime.Add(typeof(WampMessage<byte[]>), false).Add("MessageType", "Arguments");

			var messageSubber = _runtime.Add(typeof(WampDetailsOptions), true);
			var messageTypes = typeof(WampDetailsOptions).Assembly.GetTypes().Where(t => typeof(WampDetailsOptions).IsAssignableFrom(t) && !t.IsAbstract).ToList();
			for (int i = 0; i < messageTypes.Count; i++)
				messageSubber.AddSubType(i + 100, messageTypes[i]);

		}

		public ProtobufFormatter(RuntimeTypeModel runtime)
		{
			_runtime = runtime ?? TypeModel.Create();
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
			return (TTarget) Deserialize(typeof(TTarget), message);
		}

		public object Deserialize(Type type, ProtobufToken message)
		{
			using (var ms = new MemoryStream(message.Bytes, false))
				return _runtime.DeserializeWithLengthPrefix(ms, null, type, ProtoBuf.PrefixStyle.Fixed32, 0);
		}

		public void Serialize(object value, Stream stream)
		{
			_runtime.SerializeWithLengthPrefix(stream, value, value.GetType(), ProtoBuf.PrefixStyle.Fixed32, 0);
		}
		
		public ProtobufToken Serialize(object value)
		{
			using (var ms = new MemoryStream())
			{
				Serialize(value, ms);
				return new ProtobufToken { Bytes = ms.ToArray() };
			}
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
			var header = new byte[4];
			for (int i = 0; i < 4; i++)
				header[i] = (byte)stream.ReadByte();
			var messageType = (WampMessageType) BitConverter.ToInt32(header, 0);
			return new WampMessage<ProtobufToken>
			{
				MessageType = messageType,
				Arguments = _formatter.Deserialize<ProtobufToken[]>(stream)
			};

			//return _formatter.Deserialize<WampMessage<byte[]>>(stream); // WampMessageType is an invalid Enum (duplicates)
		}



		public void Format(WampMessage<object> message, Stream stream)
		{
			var header = BitConverter.GetBytes((int) message.MessageType);
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
}

