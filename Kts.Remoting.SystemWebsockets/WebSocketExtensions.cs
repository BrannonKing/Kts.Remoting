using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Kts.Remoting.Shared;

namespace Kts.Remoting.SystemWebsockets
{
	public static class ClientWebSocketExtensions
	{
		private class WebSocketTransport : ITransportSource
		{
			private static readonly CancellationToken _cancellationToken = CancellationToken.None;
			private readonly WebSocket _socket;

			public WebSocketTransport(WebSocket socket)
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
					do
					{
						if (_socket.State != WebSocketState.Open)
						{
							await Task.Delay(10);
							continue;
						}
						var result = await _socket.ReceiveAsync(segment, _cancellationToken);
						if (result.CloseStatus != WebSocketCloseStatus.Empty)
							break;
						if (result.Count > 0)
							ms.Write(segment.Array, segment.Offset, result.Count);
						if (result.EndOfMessage)
						{
							var data = new ArraySegment<byte>(ms.GetBuffer(), 0, (int) ms.Length);
							var args = new DataReceivedArgs { Data = data, SessionID = _socket.GetHashCode() };
							Received.Invoke(this, args); // assuming synchronous usage of the data: that may not be correct
							ms.SetLength(0);
						}
					} while (true);
				}
			}

			public void Dispose()
			{
			}

			public async Task Send(ArraySegment<byte> data, params object[] connectionIDs)
			{
				await _socket.SendAsync(data, WebSocketMessageType.Binary, true, _cancellationToken);
			}

			public event EventHandler<DataReceivedArgs> Received = delegate { };
		}

		public static ITransportSource GenerateTransportSource(this WebSocket socket)
		{
			return new WebSocketTransport(socket);
		}

		public static ITransportSource GenerateTransportSource(this WebSocketContext context)
		{
			// TODO: use these
			var isLocal = context.IsLocal;
			var user = context.User;
			return new WebSocketTransport(context.WebSocket);
		}
	}
}
