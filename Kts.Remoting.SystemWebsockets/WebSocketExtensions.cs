using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Kts.Remoting.SystemWebsockets
{
	public static class ClientWebSocketExtensions
	{
		private class WebSocketTransport : ICommonTransport
		{
			private static readonly CancellationToken _cancellationToken = CancellationToken.None;
			private readonly WebSocket _socket;
			private readonly bool _isText;

			public WebSocketTransport(WebSocket socket, bool isText)
			{
				_socket = socket;
				_isText = isText;
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
							var args = new DataReceivedArgs { DataCount = (int)ms.Position, Data = ms.GetBuffer() };
							ms.Position = 0;
							Received.Invoke(this, args); // assuming synchronous usage of the data: that may not be correct
						}
					} while (true);
				}
			}

			public void Dispose()
			{
				_socket.Dispose();
			}

			public async Task Send(DataToSendArgs args)
			{
				var segment = new ArraySegment<byte>(args.Data, 0, args.Data.Length);
				await _socket.SendAsync(segment, _isText ? WebSocketMessageType.Text : WebSocketMessageType.Binary, true, _cancellationToken);
			}

			public event EventHandler<DataReceivedArgs> Received = delegate { };
		}

		public static T RegisterInterface<T>(this WebSocket socket, InterfaceRegistrationOptions options) where T : class
		{
			return options.Generator.Create<T>(new WebSocketTransport(socket, options.Serializer.StreamsUtf8), options.Serializer, options.ServiceName);
		}

		public static void RegisterServices(this WebSocket socket, ServiceRegistrationOptions options)
		{
			var transport = new WebSocketTransport(socket, options.Serializer.StreamsUtf8);
			foreach (var service in options.Services)
				options.Generator.Create(transport, options.Serializer, service.Value, service.Key);
		}
	}
}
