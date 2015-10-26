using WebSocket4Net;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public static class WebSocket4NetExtensions
	{
		public static ITransportSource GenerateTransportSource(this WebSocket socket)
		{
			return new WebSocketTransportSource(socket);
		}
	}
}
