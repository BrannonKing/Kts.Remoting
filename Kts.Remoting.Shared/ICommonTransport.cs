using System;
using System.IO;
using System.Threading.Tasks;

namespace Kts.Remoting
{
	public interface ICommonTransport: IDisposable
	{
		Task Send(Stream bytes, bool binary);
		event Action<Stream> Received;
	}
}
