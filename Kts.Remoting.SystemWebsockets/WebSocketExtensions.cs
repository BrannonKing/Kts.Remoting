using System.Net;
using System.Net.WebSockets;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public static class ClientWebSocketExtensions
	{
		public static ITransportSource GenerateTransportSource(this WebSocket socket)
		{
			return new WebSocketTransportSource(socket);
		}

		public static ITransportSource GenerateTransportSource(this WebSocketContext context)
		{
			// TODO: use these
			var isLocal = context.IsLocal;
			var user = context.User;
			return new WebSocketTransportSource(context.WebSocket);
		}

		public static ITransportSource GenerateTransportSource(this HttpListener listener)
		{
			return new HttpListenerTransportSource(listener); // should have called start before here
		}
	}
}
