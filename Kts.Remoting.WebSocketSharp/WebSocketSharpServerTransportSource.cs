using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
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