using Kts.Remoting.WebSocketSharp;
using WebSocket = WebSocketSharp.WebSocket;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public static class WebSocketExtensions
	{
		public static ITransportSource GenerateTransportSource(this WebSocket socket)
		{
			return new WebSocketSharpTransport(socket);
		}
	}
}
