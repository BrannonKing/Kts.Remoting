using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer;

namespace Kts.Remoting.Client
{
	public static class ClientWebSocketExtensions
	{
		private class ClientWebSocketCommonizer : ICommonWebSocket
		{
			private readonly ConcurrentQueue<byte[]> _buffers = new ConcurrentQueue<byte[]>();
			private readonly int _bufferSize;
			private static readonly CancellationToken _cancellationToken = CancellationToken.None;
			private readonly ClientWebSocket _socket;

			public ClientWebSocketCommonizer(ClientWebSocket socket, int bufferSize = 4096)
			{
				_socket = socket;
				_bufferSize = bufferSize;
				StartReceiving();
			}

			private async void StartReceiving()
			{
				var buffer = new byte[_bufferSize];

				do
				{
					if (_socket.State != WebSocketState.Open)
					{
						await Task.Delay(10);
						continue;
					}
					using (var ms = new MemoryStream())
					{
						var segment = new ArraySegment<byte>(buffer, 0, buffer.Length);
						var result = await _socket.ReceiveAsync(segment, _cancellationToken);
						if (result.CloseStatus != WebSocketCloseStatus.Empty)
							break;
						if (result.Count > 0)
							ms.Write(segment.Array, segment.Offset, segment.Count);
						if (result.EndOfMessage)
							Received.Invoke(ms);
					}
				} while (true);
			}

			public void Dispose()
			{
				_socket.Dispose();
			}

			public async Task Send(Stream bytes, bool binary)
			{
				// a few options: 
				// 1. allocate a small array and do multiple sends
				// 2. allocate a medium array and do multiple sends
				// 3. allocate all we need to hold the stream and do a single send
				// 4. pull a medium buffer from some pool of buffers and allocate one if they're all busy; push it back on when done

				// TODO: allow an option to hard-limit the message size. (Some people like that as a security feature.)
				byte[] buffer;
				if (!_buffers.TryDequeue(out buffer))
					buffer = new byte[_bufferSize];

				while (true)
				{
					var read = bytes.Read(buffer, 0, buffer.Length);
					if (read <= 0)
						break;
					var segment = new ArraySegment<byte>(buffer, 0, read);
					await _socket.SendAsync(segment, binary ? WebSocketMessageType.Binary : WebSocketMessageType.Text, read < buffer.Length, _cancellationToken);
				}

				_buffers.Enqueue(buffer);
			}

			public event Action<Stream> Received = delegate { };
		}

		public static T RegisterInterface<T>(this ClientWebSocket socket, IProxyClassGenerator generator, ICommonSerializer serializer) where T : class
		{
			return generator.Create<T>(new ClientWebSocketCommonizer(socket),  serializer);
		}
	}
}
