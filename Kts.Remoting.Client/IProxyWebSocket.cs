using System;
using System.IO;
using System.Threading.Tasks;

namespace Kts.Remoting.Client
{
	public interface IProxyWebSocket: IDisposable
	{
		Task Send(Stream bytes, bool binary);
		event Action<ArraySegment<byte>> Received;
	}
}
