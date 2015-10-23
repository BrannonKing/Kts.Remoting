using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer.Json.NET;
using Kts.Remoting.Shared;
using vtortola.WebSockets;
using Xunit;
using WebSocket = WebSocket4Net.WebSocket;

namespace Kts.Remoting.Tests
{
	public class BasicRoundTrip
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
		public void WebSocket4NetToVtortola()
		{
			var serializer = new JsonCommonSerializer();

			var options = new WebSocketListenerOptions();
			options.SubProtocols = new[] { "unit test" };
			var listener = new WebSocketListener(new IPEndPoint(IPAddress.Loopback, 6122), options);
			var rfc6455 = new vtortola.WebSockets.Rfc6455.WebSocketFactoryRfc6455(listener);
			listener.Standards.RegisterStandard(rfc6455);
			var serverTransport = listener.GenerateTransportSource();
			var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
			serverRouter.AddService<IMyService>(new MyService());
			listener.Start();

			var client = new WebSocket("ws://localhost:6122/", "unit test", global::WebSocket4Net.WebSocketVersion.Rfc6455);
			var clientTransport = client.GenerateTransportSource();
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<IMyService>();
			client.Open();

			while (client.State != global::WebSocket4Net.WebSocketState.Open)
				Thread.Sleep(10);

			var result = proxy.Add(3, 4).Result;
			Assert.Equal(7, result);

			clientRouter.Dispose();
			clientTransport.Dispose();
			client.Dispose();

			serverRouter.Dispose();
			serverTransport.Dispose();
			listener.Dispose();
		}
	}
}
