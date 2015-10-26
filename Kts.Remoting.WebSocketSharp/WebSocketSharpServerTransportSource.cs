using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kts.Remoting.Shared;

namespace Kts.Remoting.WebSocketSharp
{
	public class WebSocketSharpServerTransportSource : ITransportSource
	{
		public void Dispose()
		{
		}

		internal void FireRecieved(DataReceivedArgs args)
		{
			Received.Invoke(this, args);
		}

		public async Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			var tasks = new List<Task>(connectionIDs.Length);
			foreach (WebSocketSharpServerBehavior socket in connectionIDs)
			{
				tasks.Add(socket.Send(data));
			}
			await Task.WhenAll(tasks);
		}

		public event EventHandler<DataReceivedArgs> Received = delegate { };
	}
}