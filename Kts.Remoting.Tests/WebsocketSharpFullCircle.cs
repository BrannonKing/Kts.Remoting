using System;
using System.Threading.Tasks;
using CommonSerializer.Json.NET;
using Kts.Remoting.Shared;
using Xunit;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Kts.Remoting.Tests
{
	public class WebsocketSharpFullCircle
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
	}
}
