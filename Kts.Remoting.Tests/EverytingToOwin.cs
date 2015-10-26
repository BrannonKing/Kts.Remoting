using System;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer.Json.NET;
using Kts.Remoting.Shared;
using Microsoft.Owin.Hosting;
using Owin;
using Xunit;

namespace Kts.Remoting.Tests
{
	public class EverytingToOwin
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

		public class Startup
		{
			public void Configuration(IAppBuilder app)
			{
				var serializer = new JsonCommonSerializer();
				var source = app.GenerateTransportSource("/rt1");
				var serverRouter = new DefaultMessageRouter(source, serializer);
				serverRouter.AddService<IMyService>(new MyService());
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
	}
}
