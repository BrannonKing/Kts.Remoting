using System;
using System.IO;
using System.Threading.Tasks;
using WebSocketSharp;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public class WebSocketSharpClientTransport : ITransportSource
	{
		protected readonly WebSocket _socket;

		public WebSocketSharpClientTransport(WebSocket socket)
		{
			_socket = socket;
			_socket.OnMessage += OnMessageReceived;
		}

		private void OnMessageReceived(object sender, MessageEventArgs e)
		{
			var args = new DataReceivedArgs { Data = new ArraySegment<byte>(e.RawData, 0, e.RawData.Length) };
			Received.Invoke(this, args);
		}

		public virtual void Dispose()
		{
			_socket.OnMessage -= OnMessageReceived;
		}

		public Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			var source = new TaskCompletionSource<bool>();
			var ms = new MemoryStream(data.Array, data.Offset, data.Count, false);
			_socket.SendAsync(ms, data.Count, success =>
			{
				ms.Dispose();
				if (success)
					source.SetResult(true);
				else
					source.SetException(new Exception("Unable to send all the data."));
			});
			return source.Task;
		}

		public event EventHandler<DataReceivedArgs> Received = delegate { };
	}
}