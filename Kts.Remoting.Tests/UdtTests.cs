using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using CommonSerializer.Newtonsoft.Json;
using CommonSerializer.ProtobufNet;
using Kts.Remoting.Benchmarks;
using Kts.Remoting.Shared;
using Xunit;
using Xunit.Abstractions;
using Socket = Udt.Socket;

namespace Kts.Remoting.Tests
{
	public class UdtTests
	{
				private readonly ITestOutputHelper _testOutputHelper;

				public UdtTests(ITestOutputHelper testOutputHelper)
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

			var listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram);
			listener.Bind(IPAddress.Loopback, port);
			listener.Listen(100);
			var serverTransport = listener.GenerateTransportSource(true);
			var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
			serverRouter.AddService<IMyService>(new MyService());

			var client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram);
			client.Connect(IPAddress.Loopback, port);
			var clientTransport = client.GenerateTransportSource(false);
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<IMyService>();

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
			//var serializerSource = new Newtonsoft.Json.JsonSerializer();
			var serializer = new ProtobufCommonSerializer();//new JsonCommonSerializer(serializerSource); // 

			var port = new Random().Next(6000, 60000);

			var listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram);
			listener.Bind(IPAddress.Loopback, port);
			listener.Listen(100);
			var serverTransport = listener.GenerateTransportSource(true);
			var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
			serverRouter.AddService<ISumService>(new SumService());

			var client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram);
			client.Connect(IPAddress.Loopback, port);
			var clientTransport = client.GenerateTransportSource(false);
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<ISumService>();

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

			clientRouter.Dispose();
			clientTransport.Dispose();
			client.Dispose();

			serverRouter.Dispose();
			serverTransport.Dispose();
			listener.Dispose();
		}

	}
}
