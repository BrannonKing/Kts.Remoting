using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Kts.Remoting
{
	public sealed class DataReceivedArgs : EventArgs
	{
		public byte[] Data { get; set; }
		public int DataCount { get; set; }
		public byte[] ConnectionID { get; set; }
		public IIdentity Identity { get; set; }
	}

	public sealed class DataToSendArgs : EventArgs
	{
		public byte[] Data { get; set; }
		public IEnumerable<byte[]> ConnectionIDs { get; set; }
	}

	public interface ICommonTransport : IDisposable
	{
		Task Send(DataToSendArgs args);
		event EventHandler<DataReceivedArgs> Received;
	}
}
