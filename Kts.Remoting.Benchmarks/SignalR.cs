using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using Owin;
using Xunit;
using Xunit.Abstractions;

namespace Kts.Remoting.Benchmarks
{
	public class SignalR
	{
		private readonly ITestOutputHelper _testOutputHelper;

		public SignalR(ITestOutputHelper testOutputHelper)
		{
			_testOutputHelper = testOutputHelper;
		}

		[Fact]
		public void BenchmarkMessages()
		{
			var port = new Random().Next(20000, 60000);

			GlobalHost.Configuration.DefaultMessageBufferSize = 2000; // maximum number of messages to buffer
			GlobalHost.Configuration.MaxIncomingWebSocketMessageSize = null;
			GlobalHost.Configuration.DisconnectTimeout = TimeSpan.FromSeconds(15); // auto set keepalive to 5

			var url = "http://localhost:" + port + "/";
			var server = WebApp.Start<Startup>(url);
			var client = new HubConnection(url + "s1");
			var proxy = client.CreateHubProxy("ISumService");
			client.Start().Wait();

			Assert.Equal(ConnectionState.Connected, client.State);

			const int randCnt = 100;
			var rand = new Random(42);
			var randoms = new int[randCnt];
			for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);

			var sw = new Stopwatch();
			for (int j = 0; j < 500; j++)
			{
				sw.Start();
				var sum = proxy.Invoke<int>("Sum", randoms).Result;
				sw.Stop();
				Assert.Equal(randoms.Sum(), sum);
				for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);
			}

			_testOutputHelper.WriteLine("Completed 500 sum passes in {0}ms", sw.Elapsed.TotalMilliseconds);

			sw.Reset();
			var tree = new SumServiceTree();
			SumServiceTree.FillTree(tree, rand, 2);
			_testOutputHelper.WriteLine("Starting large message transfer.");
			sw.Start();
			var result = proxy.Invoke<SumServiceTree>("Increment", tree).Result;
			sw.Stop();
			Assert.Equal(tree.Leaf + 1, result.Leaf);
			_testOutputHelper.WriteLine("Completed large transfer in {0}ms", sw.Elapsed.TotalMilliseconds);

			client.Dispose();
			server.Dispose();
		}

		public class Startup
		{
			public void Configuration(IAppBuilder app)
			{
				app.MapSignalR("/s1", new HubConfiguration { EnableDetailedErrors = true, EnableJavaScriptProxies = false, EnableJSONP = false });
			}
		}
	}

	[HubName("ISumService")]
	public class SumServiceHub : Hub, ISumService
	{
		private readonly SumService _service = new SumService();

		public Task<int> Sum(int[] values)
		{
			return _service.Sum(values);
		}

		public Task<SumServiceTree> Increment(SumServiceTree tree)
		{
			return _service.Increment(tree);
		}
	}

}
