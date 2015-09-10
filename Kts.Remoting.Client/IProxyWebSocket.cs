using System;
using System.Threading.Tasks;

namespace Kts.Remoting.Client
{
	public interface IProxyWebSocket: IDisposable
	{
		Task Send(byte[] bytes, bool binary);
		event Action<ArraySegment<byte>> Received;
	}
}
