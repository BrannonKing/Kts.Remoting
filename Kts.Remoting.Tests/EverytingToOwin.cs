using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer.Json.NET;
using CommonSerializer.ProtobufNet;
using Kts.Remoting.Benchmarks;
using Kts.Remoting.Shared;
using Microsoft.Owin.Hosting;
using Owin;
using Xunit;
using Xunit.Abstractions;

namespace Kts.Remoting.Tests
{
	public class EverytingToOwin
	{
		private readonly ITestOutputHelper _testOutputHelper;

		public EverytingToOwin(ITestOutputHelper testOutputHelper)
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

		public class Startup
		{
			public void Configuration(IAppBuilder app)
			{
				var serializer = new JsonCommonSerializer(); // new ProtobufCommonSerializer(); // 
				var source = app.GenerateTransportSource("/rt1");
				var serverRouter = new DefaultMessageRouter(source, serializer);
				serverRouter.AddService<IMyService>(new MyService());
				serverRouter.AddService<ISumService>(new SumService());
			}
		}

		[Fact]
		public void WebSocket4NetRoundTrip()
		{
			var serializer = new JsonCommonSerializer();

			var port = new Random().Next(20000, 60000);

			var server = WebApp.Start<Startup>("http://localhost:" + port + "/");

			var client = new WebSocket4Net.WebSocket("ws://localhost:" + port + "/rt1", "", WebSocket4Net.WebSocketVersion.Rfc6455);
			var clientTransport = client.GenerateTransportSource();
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<IMyService>();
			client.Open();

			while (client.State != WebSocket4Net.WebSocketState.Open)
				Thread.Sleep(10);

			var result = proxy.Add(3, 4).Result;
			Assert.Equal(7, result);

			clientRouter.Dispose();
			clientTransport.Dispose();
			client.Dispose();

			server.Dispose();
		}

		[Fact]
		public void WebSocketSharpRoundTrip()
		{
			var serializer = new JsonCommonSerializer();

			var port = new Random().Next(20000, 60000);

			var server = WebApp.Start<Startup>("http://localhost:" + port + "/");

			var client = new WebSocketSharp.WebSocket("ws://localhost:" + port + "/rt1");
			var clientTransport = client.GenerateTransportSource();
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<IMyService>();
			client.Connect();

			var result = proxy.Add(3, 4).Result;
			Assert.Equal(7, result);

			clientRouter.Dispose();
			clientTransport.Dispose();
			client.Close();

			server.Dispose();
		}

		[Fact]
		public void BenchmarkMessagesWebSocketSharp()
		{
			var serializer = new JsonCommonSerializer(); // new ProtobufCommonSerializer(); // 
			var port = new Random().Next(20000, 60000);

			var url = "http://localhost:" + port + "/";
			var server = WebApp.Start<Startup>(url);

			var client = new WebSocketSharp.WebSocket("ws://localhost:" + port + "/rt1");
			var clientTransport = client.GenerateTransportSource();
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<ISumService>();
			client.Connect();

			const int randCnt = 100;
			var rand = new Random(42);
			var randoms = new int[randCnt];
			for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);

			var sw = new Stopwatch();
			long timeFromClient = 0, timeToClient = 0;
			const int cnt = 1000;
			for (int j = 0; j < cnt; j++)
			{
				sw.Start();
				var sum = proxy.Sum(randoms).Result;
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

			client.Close();
			server.Dispose();
		}

	}
}
