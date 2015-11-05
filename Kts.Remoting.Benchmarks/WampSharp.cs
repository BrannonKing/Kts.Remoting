using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using NetMQ;
using Newtonsoft.Json;
using ProtoBuf.Meta;
using WampSharp.Binding;
using WampSharp.Core.Message;
using WampSharp.Core.Serialization;
using WampSharp.Vtortola;
using WampSharp.V2;
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
			long timeFromClient = 0, timeToClient = 0;
			const int cnt = 1000;
			for (int j = 0; j < cnt; j++)
			{
				sw.Start();
				var sum = proxy.SumPackage(package).Result;
				sw.Stop();
				Assert.Equal(randoms.Sum(), sum);
				for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);
				var times = proxy.TimeDiff(Stopwatch.GetTimestamp()).Result;
				timeFromClient += times.Item1;
				timeToClient += Stopwatch.GetTimestamp() - times.Item2;
			}

			_testOutputHelper.WriteLine("Completed {0} sum passes in {1}ms", cnt, sw.ElapsedMilliseconds);
			_testOutputHelper.WriteLine("Client to server latency: {0}us", timeFromClient / cnt / 10);
			_testOutputHelper.WriteLine("Server to client latency: {0}us", timeToClient / cnt / 10);

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
			long timeFromClient = 0, timeToClient = 0;
			const int cnt = 1000;
			for (int j = 0; j < cnt; j++)
			{
				sw.Start();
				var sum = proxy.SumPackage(package).Result;
				sw.Stop();
				Assert.Equal(randoms.Sum(), sum);
				for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);
				var times = proxy.TimeDiff(Stopwatch.GetTimestamp()).Result;
				timeFromClient += times.Item1;
				timeToClient += Stopwatch.GetTimestamp() - times.Item2;
			}

			_testOutputHelper.WriteLine("Completed {0} sum passes in {1}ms", cnt, sw.ElapsedMilliseconds);
			_testOutputHelper.WriteLine("Client to server latency: {0}us", timeFromClient / cnt / 10);
			_testOutputHelper.WriteLine("Server to client latency: {0}us", timeToClient / cnt / 10);

			sw.Reset();
			var tree = new SumServiceTree();
			SumServiceTree.FillTree(tree, rand, 2);
			_testOutputHelper.WriteLine("Starting large message transfer.");
			sw.Start();
			var result = proxy.Increment(tree).Result;
			sw.Stop();
			Assert.True(tree.IsExactMatch(result, 1));
			_testOutputHelper.WriteLine("Completed large transfer in {0}ms", sw.Elapsed.TotalMilliseconds);

			channel.Close();
			host.Dispose();
		}

		[Fact]
		public void BenchmarkMessagesProtobufPlusNetMQ()
		{
			var host = StartServer();
			var factory = new WampChannelFactory();
			var binding = new ProtobufBinding();
			var connection = new BinaryNetMQConnection<ProtobufToken>(binding, );
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
			long timeFromClient = 0, timeToClient = 0;
			const int cnt = 1000;
			for (int j = 0; j < cnt; j++)
			{
				sw.Start();
				var sum = proxy.SumPackage(package).Result;
				sw.Stop();
				Assert.Equal(randoms.Sum(), sum);
				for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);
				var times = proxy.TimeDiff(Stopwatch.GetTimestamp()).Result;
				timeFromClient += times.Item1;
				timeToClient += Stopwatch.GetTimestamp() - times.Item2;
			}

			_testOutputHelper.WriteLine("Completed {0} sum passes in {1}ms", cnt, sw.ElapsedMilliseconds);
			_testOutputHelper.WriteLine("Client to server latency: {0}us", timeFromClient / cnt / 10);
			_testOutputHelper.WriteLine("Server to client latency: {0}us", timeToClient / cnt / 10);

			sw.Reset();
			var tree = new SumServiceTree();
			SumServiceTree.FillTree(tree, rand, 2);
			_testOutputHelper.WriteLine("Starting large message transfer.");
			sw.Start();
			var result = proxy.Increment(tree).Result;
			sw.Stop();
			Assert.True(tree.IsExactMatch(result, 1));
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

		private static IDisposable StartNetMQServer()
		{
			var host = new WampHost();

			var serverContext = NetMQContext.Create();
			var serverSocket = serverContext.CreateResponseSocket();// serverContext.CreateRouterSocket();
			var serverPoller = new Poller(serverSocket);
			var serverScheduler = new NetMQScheduler(serverContext, serverPoller);
			serverPoller.PollTillCancelledNonBlocking();

			host.RegisterTransport(new NetMQTransport(serverSocket, serverScheduler),
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
}

