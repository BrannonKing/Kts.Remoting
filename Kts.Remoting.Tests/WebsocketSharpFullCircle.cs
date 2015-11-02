using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommonSerializer.Json.NET;
using CommonSerializer.ProtobufNet;
using Kts.Remoting.Benchmarks;
using Kts.Remoting.Shared;
using Xunit;
using WebSocketSharp.Server;
using Xunit.Abstractions;
using WebSocket = WebSocketSharp.WebSocket;

namespace Kts.Remoting.Tests
{
	public class WebsocketSharpFullCircle
	{
		private readonly ITestOutputHelper _testOutputHelper;

		public WebsocketSharpFullCircle(ITestOutputHelper testOutputHelper)
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

			var listener = new WebSocketServer("ws://localhost:" + port);
			var serverTransport = listener.GenerateTransportSource("/p1");
			var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
			serverRouter.AddService<IMyService>(new MyService());
			listener.Start();

			var client = new WebSocket("ws://localhost:" + port + "/p1");
			var clientTransport = client.GenerateTransportSource();
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<IMyService>();
			client.Connect();

			var result = proxy.Add(3, 4).Result;
			Assert.Equal(7, result);

			clientRouter.Dispose();
			clientTransport.Dispose();
			client.Close();

			serverRouter.Dispose();
			serverTransport.Dispose();
			listener.Stop();
		}

		[Fact]
		public void Benchmark()
		{
			var serializerSource = new Newtonsoft.Json.JsonSerializer();
			var serializer = new ProtobufCommonSerializer();//new JsonCommonSerializer(serializerSource); // 

			var port = new Random().Next(6000, 60000);

			var listener = new WebSocketServer("ws://localhost:" + port);
			var serverTransport = listener.GenerateTransportSource("/p1");
			var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
			serverRouter.AddService<ISumService>(new SumService());
			listener.Start();

			var client = new WebSocket("ws://localhost:" + port + "/p1");
			//client.Compression = WebSocketSharp.CompressionMethod.Deflate;
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

			//sw.Reset();
			//var tree = new SumServiceTree();
			//SumServiceTree.FillTree(tree, rand, 2);
			//_testOutputHelper.WriteLine("Starting large message transfer.");
			//sw.Start();
			//var result = proxy.Increment(tree).Result;
			//sw.Stop();
			//Assert.Equal(tree.Leaf + 1, result.Leaf);
			//_testOutputHelper.WriteLine("Completed large transfer in {0}ms", sw.Elapsed.TotalMilliseconds);

			clientRouter.Dispose();
			clientTransport.Dispose();
			client.Close();

			serverRouter.Dispose();
			serverTransport.Dispose();
			listener.Stop();
		}

	}
}
