using System;
using System.Threading.Tasks;
using WebSocket4Net;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public class WebSocketTransportSource : ITransportSource
	{
		private readonly WebSocket _socket;

		public WebSocketTransportSource(WebSocket socket)
		{
			_socket = socket;
			_socket.DataReceived += OnDataReceived;
		}

		private void OnDataReceived(object sender, DataReceivedEventArgs e)
		{
			var args = new DataReceivedArgs();
			args.Data = new ArraySegment<byte>(e.Data, 0, e.Data.Length);
			Received.Invoke(this, args);
		}

		public void Dispose()
		{
			_socket.DataReceived -= OnDataReceived;
		}

		public Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			return Task.Run(() => _socket.Send(data.Array, data.Offset, data.Count)); // not sure if we really need the task here
		}

		public event EventHandler<DataReceivedArgs> Received = delegate { };
	}
}