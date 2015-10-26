using WebSocketSharp.Server;
using WebSocket = WebSocketSharp.WebSocket;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public static class WebSocketExtensions
	{
		public static ITransportSource GenerateTransportSource(this WebSocket socket)
		{
			return new WebSocketSharpClientTransport(socket);
		}

		public static ITransportSource GenerateTransportSource(this WebSocketServer socket, string path)
		{
			var source = new WebSocketSharpServerTransportSource();
			socket.AddWebSocketService(path, () => new WebSocketSharpServerBehavior(source));
			return source;
		}

	}
}
