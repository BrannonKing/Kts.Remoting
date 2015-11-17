using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer.Newtonsoft.Json;
using Kts.Remoting.Shared;
using Xunit;

namespace Kts.Remoting.Tests
{
	public class SystemWebSocketsFullCircle
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

			var server = new HttpListener();
			server.Prefixes.Add("http://localhost:20000/");
			server.Start();
			var serverTransport = server.GenerateTransportSource();
			var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
			serverRouter.AddService<IMyService>(new MyService());

			var client = new ClientWebSocket();
			client.Options.SetBuffer(8192, 8192);
			var clientTransport = client.GenerateTransportSource();
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<IMyService>();

			client.ConnectAsync(new Uri("ws://localhost:20000/"), CancellationToken.None).Wait();

			var result = proxy.Add(3, 4).Result;
			Assert.Equal(7, result);

			clientRouter.Dispose();
			clientTransport.Dispose();
			client.Dispose();

			serverRouter.Dispose();
			serverTransport.Dispose();
			server.Stop();
			server.Close();
		}
	}
}
