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
	}
}
