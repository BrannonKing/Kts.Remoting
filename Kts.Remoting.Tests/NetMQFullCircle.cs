using System;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer;
using CommonSerializer.Json.NET;
using Kts.Remoting.Shared;
using Xunit;
using NetMQ;
using NetMQ.Monitoring;

namespace Kts.Remoting.Tests
{
	public class NetMQFullCircle
	{
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

		private Task RunServer(ICommonSerializer serializer, Action<int> loadPort, Action<Poller> loadPoller)
		{
			var task = new Task(() =>
			{
				var serverContext = NetMQContext.Create();
				var serverSocket = serverContext.CreateResponseSocket();// serverContext.CreateRouterSocket();
				var serverPoller = new Poller(serverSocket);
				var serverScheduler = new NetMQScheduler(serverContext, serverPoller);
				var serverTransport = serverSocket.GenerateTransportSource(serverScheduler);
				var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
				serverRouter.AddService<IMyService>(new MyService());
				var port = serverSocket.BindRandomPort("tcp://127.0.0.1");
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

		private Task RunClient(ICommonSerializer serializer, int port, Action<Poller> loadPoller, Action<IMyService> loadProxy)
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
				var proxy = clientRouter.AddInterface<IMyService>();
				clientSocket.Connect("tcp://127.0.0.1:" + port);
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
			var serverThread = RunServer(serializer, p => port = p, p => server = p);
			while (port == -1)
				Thread.Yield();
			IMyService proxy = null;
			var clientThread = RunClient(serializer, port, p => client = p, p => proxy = p);

			while(proxy == null)
				Thread.Yield();

			var result = proxy.Add(3, 4).Result;
			Assert.Equal(7, result);

			client.CancelAndJoin();
			server.CancelAndJoin();

			clientThread.Wait();
			serverThread.Wait();
		}
	}
}
