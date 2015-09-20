using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer.Json.NET;
using Kts.Remoting.Client;
using Microsoft.Owin.Hosting;
using Owin;
using Xunit;

namespace Kts.Remoting.Tests
{
	public class OwinClientServerTests
	{
		[Fact]
		public void ConsecutiveCalls()
		{
			const string address = "http://localhost:18081/";
			using (WebApp.Start<Startup>(address))
			using (var client = new ClientWebSocket())
			{
				var generator = new DynamicProxyClassGenerator();// new CodeDomProxyClassGenerator();
				var service = client.RegisterInterface<IMyService>(generator, new JsonCommonSerializer());

				client.ConnectAsync(new Uri("ws://localhost:18081/rt1"), CancellationToken.None).Wait();

				var result = service.Add(3, 4).Result;
				Assert.Equal(7, result);
			}
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
			public readonly MyService Service = new MyService();

			public void Configuration(IAppBuilder app)
			{
				var options = new OptionsForProxiedServices();
				options.AddService<IMyService>(Service);
				options.Serializer = new JsonCommonSerializer();

				app.AddProxiedServices("/rt1", options);
			}
		}

	}
}
