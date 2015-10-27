using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public class WebSocketTransportSource : ITransportSource
	{
		private static readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
		private readonly WebSocket _socket;

		public WebSocketTransportSource(WebSocket socket)
		{
			_socket = socket;
			StartReceiving();
		}

		private async void StartReceiving()
		{
			var buffer = new byte[8192];
			var segment = new ArraySegment<byte>(buffer, 0, buffer.Length);
			using (var ms = new MemoryStream())
			{
				try
				{
					do
					{
						if (_socket.State != WebSocketState.Open)
						{
							await Task.Delay(10);
							continue;
						}
						var result = await _socket.ReceiveAsync(segment, _tokenSource.Token);
						if (result.CloseStatus != null && result.CloseStatus != WebSocketCloseStatus.Empty)
							break;
						if (result.Count > 0)
							ms.Write(segment.Array, segment.Offset, result.Count);
						if (result.EndOfMessage)
						{
							var data = new ArraySegment<byte>(ms.GetBuffer(), 0, (int) ms.Length);
							var args = new DataReceivedArgs { Data = data, SessionID = _socket };
							Received.Invoke(this, args); // assuming synchronous usage of the data: that may not be correct
							ms.SetLength(0);
						}
					} while (!_tokenSource.IsCancellationRequested);
				}
				catch (OperationCanceledException)
				{
					// exit without propagating the exception
				}
			}
		}

		public void Dispose()
		{
			_tokenSource.Cancel();
		}

		public async Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			var tasks = new List<Task>(connectionIDs.Length);
			foreach (var socket in connectionIDs)
			{
				if (socket is WebSocket)
				{
					tasks.Add(_socket.SendAsync(data, WebSocketMessageType.Binary, true, _tokenSource.Token));
				}
			}

			if (tasks.Count > 0)
				await Task.WhenAll(tasks);
			else
				await _socket.SendAsync(data, WebSocketMessageType.Binary, true, _tokenSource.Token);
		}

		public event EventHandler<DataReceivedArgs> Received = delegate { };
	}
}