using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Xunit;
using Xunit.Abstractions;

namespace Kts.Remoting.Benchmarks
{
	public class WCF
	{
		private readonly ITestOutputHelper _testOutputHelper;

		public WCF(ITestOutputHelper testOutputHelper)
		{
			_testOutputHelper = testOutputHelper;
		}

		[Fact]
		public void BenchmarkMessagesSoap()
		{
			string baseAddress = "http://localhost:12292/";
			var host = new ServiceHost(typeof(WcfWumService), new Uri(baseAddress));
			host.AddServiceEndpoint(typeof(IWcfSumService), new NetHttpBinding { MaxReceivedMessageSize = int.MaxValue }, "soap");
			host.Open();

			var factory = new ChannelFactory<IWcfSumService>(new NetHttpBinding { MaxReceivedMessageSize = int.MaxValue }, new EndpointAddress(baseAddress + "soap"));
			var proxy = factory.CreateChannel();

			const int randCnt = 100;
			var rand = new Random(42);
			var randoms = new int[randCnt];
			for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);

			var sw = new Stopwatch();
			for (int j = 0; j < 500; j++)
			{
				sw.Start();
				var sum = proxy.Sum(randoms).Result;
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
			var result = proxy.Increment(tree).Result;
			sw.Stop();
			Assert.Equal(tree.Leaf + 1, result.Leaf);
			_testOutputHelper.WriteLine("Completed large transfer in {0}ms", sw.Elapsed.TotalMilliseconds);

			((IDisposable)proxy).Dispose();
			factory.Close();
			host.Close();
		}

	}

	[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.PerCall)]
	public class WcfWumService : IWcfSumService
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

	[ServiceContract]
	public interface IWcfSumService
	{
		[OperationContract]
		Task<int> Sum(int[] values);

		[OperationContract]
		Task<SumServiceTree> Increment(SumServiceTree tree);
	}

}
