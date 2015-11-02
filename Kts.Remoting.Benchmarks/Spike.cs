using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Spike;
using Spike.Hubs;
using Xunit;

namespace Kts.Remoting.Benchmarks
{
	public class SumServiceSpikeHub : Hub, ISumService
	{
		private readonly SumService _service = new SumService();

		public Task<Tuple<long, long>> TimeDiff(long stamp)
		{
			return _service.TimeDiff(stamp);
		}

		public Task<int> SumPackage(SumPackage package)
		{
			return Sum(package.Numbers);
		}

		public Task<int> Sum(int[] values)
		{
			return _service.Sum(values);
		}

		public Task<SumServiceTree> Increment(SumServiceTree tree)
		{
			return _service.Increment(tree);
		}
	}

	public class Spike
	{
		[Fact]
		public void Benchmark()
		{
			var hub = new SumServiceSpikeHub();
			Service.Listen(new TcpBinding(IPAddress.Any, 8002)); // or NetBinding


			Service.Shutdown();
			hub.Dispose();
		}
	}
}
