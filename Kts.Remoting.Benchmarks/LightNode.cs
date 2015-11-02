using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LightNode.Formatter;
using LightNode.Server;
using Microsoft.Owin.Hosting;
using Owin;
using Xunit;

namespace Kts.Remoting.Benchmarks
{
	public class LightNode
	{
		[Fact]
		public void Benchmark()
		{
			var port = new Random().Next(20000, 60000);
			var url = "http://localhost:" + port + "/";
			var server = WebApp.Start<Startup>(url);

			var client = new HttpClient();
			var clientFormatter = new JsonNetContentFormatter();

			var ints = new int[] { 2, 3 };

			using (var ms = new MemoryStream())
			{
				clientFormatter.Serialize(ms, ints); // yes, they really close the stream
				ms.Position = 0;
				using (var content = new StreamContent(ms))
				{
					var result = client.PostAsync(url + "Sum", content).Result.Content.ReadAsStreamAsync().Result;
					var two = clientFormatter.Deserialize(typeof(int), result);
					Assert.Equal(5, two);
				}
			}

			server.Dispose();
		}
		public class Startup
		{
			public void Configuration(IAppBuilder app)
			{
				app.UseLightNode(new LightNodeOptions(
					AcceptVerbs.Get | AcceptVerbs.Post,
					new JsonNetContentFormatter()
					//,new GZipJavaScriptContentFormatter()
					//,new ProtoBufContentFormatter()
					));
			}
		}
	}


	public class LNSumService : LightNodeContract
	{
		private static readonly SumService _service = new SumService();

		public Task<Tuple<long, long>> TimeDiff(long stamp)
		{
			return _service.TimeDiff(stamp);
		}

		//public Task<int> SumPackage(SumPackage package)
		//{
		//	return _service.SumPackage(package);
		//}

		public Task<int> Sum(int[] values)
		{
			return _service.Sum(values);
		}

		//public Task<SumServiceTree> Increment(SumServiceTree tree)
		//{
		//	return _service.Increment(tree);
		//}
	}
}
