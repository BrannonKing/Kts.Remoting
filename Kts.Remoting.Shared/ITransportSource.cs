using System;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Kts.Remoting
{
	public sealed class DataReceivedArgs : EventArgs
	{
		public ArraySegment<byte> Data { get; set; }
		public object SessionID { get; set; }
		public IIdentity Identity { get; set; }
	}

	public interface ITransportSource : IDisposable
	{
		Task Send(ArraySegment<byte> data, params object[] connectionIDs);
		event EventHandler<DataReceivedArgs> Received;
	}
}
