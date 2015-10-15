using System;
using System.IO;
using System.Threading.Tasks;
using CommonSerializer;
using WebSocketSharp;
using WebSocket = WebSocketSharp.WebSocket;

namespace Kts.Remoting.Client.WebSocketSharp
{
	public static class WebSocketExtensions
	{
		private class WebSocketSharpComonizer : ICommonTransport
		{
			private readonly WebSocket _socket;

			public WebSocketSharpComonizer(WebSocket socket)
			{
				_socket = socket;
				_socket.OnMessage += OnMessageReceived;
			}

			private void OnMessageReceived(object sender, MessageEventArgs e)
			{
				using (var ms = new MemoryStream(e.RawData))
					Received.Invoke(ms);
			}

			public void Dispose()
			{
				_socket.OnMessage -= OnMessageReceived;
			}

			public Task Send(Stream stream, bool binary)
			{
				var source = new TaskCompletionSource<bool>();
				_socket.SendAsync(stream, (int)stream.Length, success =>
				{
					if (success)
						source.SetResult(true);
					else
						source.SetException(new Exception("Unable to send all the data."));
				});
				return source.Task;
			}

			public event Action<Stream> Received = delegate { };
		}
		public static void RegisterInterface<T>(this WebSocket socket, IProxyClassGenerator generator, ICommonSerializer serializer) where T : class
		{
			generator.Create<T>(new WebSocketSharpComonizer(socket),  serializer);
		}

	}
}
