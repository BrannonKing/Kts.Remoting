using vtortola.WebSockets;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public static class VtortolaExtensions
	{
		public static ITransportSource GenerateTransportSource(this WebSocketListener listener)
		{
			return new WebSocketListenerTransportSource(listener);
		}
	}
}
