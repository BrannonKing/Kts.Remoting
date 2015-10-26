using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using CommonSerializer.Json.NET;
using Kts.Remoting.Shared;
using Kts.Remoting.SystemWebsockets;
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
		public void ContextualServer()
		{
			var serializer = new JsonCommonSerializer();

			var listener = new HttpListener();
			listener.Prefixes.Add("http://127.0.0.1:20000/");
			listener.Start();

			var task = StartListening(listener);

			var client = new ClientWebSocket();
			var clientTransport = client.GenerateTransportSource();
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<IMyService>();

			client.ConnectAsync(new Uri("ws://127.0.0.1:20000/"), CancellationToken.None).Wait();

			var result = proxy.Add(3, 4).Result;
			Assert.Equal(7, result);

			clientRouter.Dispose();
			clientTransport.Dispose();
			client.Dispose();

		}

		private async Task StartListening(HttpListener listener)
		{
			var context = await listener.GetContextAsync();
			if (context.Request.IsWebSocketRequest
			do
			{
				var socket = await context.AcceptWebSocketAsync("unit test");
				if (!socket.Request.IsWebSocketRequest
				var source = socket.GenerateTransportSource();
			} while ();



		}
	}
}
