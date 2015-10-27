using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer.Json.NET;
using Kts.Remoting.Benchmarks;
using Kts.Remoting.Shared;
using vtortola.WebSockets;
using Xunit;
using Xunit.Abstractions;

namespace Kts.Remoting.Tests
{
	public class SystemWebSocketsToVtortolaCircle
	{
		private readonly ITestOutputHelper _testOutputHelper;

		public SystemWebSocketsToVtortolaCircle(ITestOutputHelper testOutputHelper)
		{
			_testOutputHelper = testOutputHelper;
		}

		public interface IMyService
		{
			Task<int> Add(int a, int b);
		}

		public class MyService : IMyService
		{
			public Task<int> Add(int a, int b)
			{
				return Task.FromResult(a + b);
			}
		}

		[Fact]
		public void BasicRoundTrip()
		{
			var serializer = new JsonCommonSerializer();

			var port = new Random().Next(6000, 60000);

			var options = new WebSocketListenerOptions();
			options.SubProtocols = new[] { "SignalR" };
			var listener = new WebSocketListener(new IPEndPoint(IPAddress.Loopback, port), options);
			var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(listener);
			listener.Standards.RegisterStandard(rfc6455);
			var serverTransport = listener.GenerateTransportSource();
			var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
			serverRouter.AddService<IMyService>(new MyService());
			listener.Start();

			var client = new ClientWebSocket();
			client.Options.AddSubProtocol("SignalR");
			var clientTransport = client.GenerateTransportSource();
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<IMyService>();
			client.ConnectAsync(new Uri("ws://localhost:" + port + "/"), CancellationToken.None).Wait();

			var result = proxy.Add(3, 4).Result;
			Assert.Equal(7, result);

			clientRouter.Dispose();
			clientTransport.Dispose();
			client.Dispose();

			serverRouter.Dispose();
			serverTransport.Dispose();
			listener.Dispose();
		}

		[Fact]
		public void Benchmark()
		{
			var serializer = new JsonCommonSerializer();

			var port = new Random().Next(6000, 60000);

			var options = new WebSocketListenerOptions();
			options.SubProtocols = new[] { "SignalR" };
			var listener = new WebSocketListener(new IPEndPoint(IPAddress.Loopback, port), options);
			var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(listener);
			listener.Standards.RegisterStandard(rfc6455);
			var serverTransport = listener.GenerateTransportSource();
			var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
			serverRouter.AddService<ISumService>(new SumService());
			listener.Start();

			var client = new ClientWebSocket();
			client.Options.AddSubProtocol("SignalR");
			var clientTransport = client.GenerateTransportSource();
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<ISumService>();
			client.ConnectAsync(new Uri("ws://localhost:" + port + "/"), CancellationToken.None).Wait();

			const int randCnt = 100;
			var rand = new Random(42);
			var randoms = new int[randCnt];
			for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);

			var sw = new Stopwatch();
			for (int j = 0; j < 500; j++)
			{
				sw.Start();
				var sum = proxy.Sum(randoms).Result;
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

			clientRouter.Dispose();
			clientTransport.Dispose();
			client.Dispose();

			serverRouter.Dispose();
			serverTransport.Dispose();
			listener.Dispose();
		}
	}
}
