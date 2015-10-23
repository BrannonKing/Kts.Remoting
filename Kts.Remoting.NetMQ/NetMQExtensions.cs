using Kts.Remoting.NetMQ;
using NetMQ;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public static class WebSocketExtensions
	{
		public static ITransportSource GenerateTransportSource(this NetMQSocket socket, NetMQScheduler scheduler)
		{
			return new NetMQSocketTransportSource(socket, scheduler);
		}

		public static ITransportSource GenerateTransportSource(this NetMQContext context, NetMQSocket socket, Poller poller = null)
		{
			return new NetMQSocketTransportSource(context, socket, poller);
		}
	}
}
