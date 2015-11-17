using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer;
using CommonSerializer.Newtonsoft.Json;
using Kts.Remoting.Benchmarks;
using Kts.Remoting.Shared;
using Xunit;
using NetMQ;
using Xunit.Abstractions;

namespace Kts.Remoting.Tests
{
	public class NetMQFullCircle
	{
		private readonly ITestOutputHelper _testOutputHelper;

		public NetMQFullCircle(ITestOutputHelper testOutputHelper)
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

		private Task RunServer<T>(ICommonSerializer serializer, Action<int> loadPort, Action<Poller> loadPoller, T service)
			where T:class
		{
			var task = new Task(() =>
			{
				var serverContext = NetMQContext.Create();
				var serverSocket = serverContext.CreateResponseSocket();// serverContext.CreateRouterSocket();
				var serverPoller = new Poller(serverSocket);
				var serverScheduler = new NetMQScheduler(serverContext, serverPoller);
				var serverTransport = serverSocket.GenerateTransportSource(serverScheduler);
				var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
				serverRouter.AddService(service);
				var port = serverSocket.BindRandomPort("tcp://localhost");
				loadPoller.Invoke(serverPoller);
				loadPort.Invoke(port);

				serverPoller.PollTillCancelled();

				serverScheduler.Dispose();
				serverPoller.Dispose();
				serverRouter.Dispose();
				serverTransport.Dispose();
				serverSocket.Dispose();
				serverContext.Dispose();
			}, TaskCreationOptions.LongRunning);
			task.Start();
			return task;
		}

		private Task RunClient<T>(ICommonSerializer serializer, int port, Action<Poller> loadPoller, Action<T> loadProxy)
			where T:class
		{
			var task = new Task(() =>
			{
				var clientContext = NetMQContext.Create();
				var clientSocket = clientContext.CreateRequestSocket();// clientContext.CreateRouterSocket();
				clientSocket.Options.Linger = TimeSpan.Zero;
				var clientPoller = new Poller(clientSocket);
				var clientScheduler = new NetMQScheduler(clientContext, clientPoller);
				var clientTransport = clientSocket.GenerateTransportSource(clientScheduler);
				var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
				var proxy = clientRouter.AddInterface<T>();
				clientSocket.Connect("tcp://localhost:" + port);
				loadPoller.Invoke(clientPoller);
				loadProxy.Invoke(proxy);

				clientPoller.PollTillCancelled();

				clientScheduler.Dispose();
				clientPoller.Dispose();
				clientRouter.Dispose();
				clientTransport.Dispose();
				clientSocket.Dispose();
				clientContext.Dispose();
			}, TaskCreationOptions.LongRunning);
			task.Start();
			return task;
		}

		[Fact]
		public void BasicRoundTrip()
		{
			var serializer = new JsonCommonSerializer();
			int port = -1;
			Poller client = null, server = null;
			var serverThread = RunServer<IMyService>(serializer, p => port = p, p => server = p, new MyService());
			while (port == -1)
				Thread.Yield();
			IMyService proxy = null;
			var clientThread = RunClient<IMyService>(serializer, port, p => client = p, p => proxy = p);

			while(proxy == null)
				Thread.Yield();

			var result = proxy.Add(3, 4).Result;
			Assert.Equal(7, result);

			client.CancelAndJoin();
			server.CancelAndJoin();

			clientThread.Wait();
			serverThread.Wait();
		}

		[Fact]
		public void Benchmark()
		{
			var serializer = new JsonCommonSerializer(); // new ProtobufCommonSerializer();// 
			int port = -1;
			Poller client = null, server = null;
			var serverThread = RunServer<ISumService>(serializer, p => port = p, p => server = p, new SumService());
			while (port == -1)
				Thread.Yield();
			ISumService proxy = null;
			var clientThread = RunClient<ISumService>(serializer, port, p => client = p, p => proxy = p);

			while (proxy == null)
				Thread.Yield();

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
			Assert.True(tree.IsExactMatch(result, 1));
			_testOutputHelper.WriteLine("Completed large transfer in {0}ms", sw.Elapsed.TotalMilliseconds);

			client.CancelAndJoin();
			server.CancelAndJoin();

			clientThread.Wait();
			serverThread.Wait();
		}
	}
}
