using System;
using System.IO;
using System.Threading.Tasks;
using CommonSerializer;
using WebSocketSharp;
using Kts.Remoting;
using WebSocket = WebSocketSharp.WebSocket;

namespace Kts.Remoting.WebSocketSharp
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
				e.
				using (var ms = new MemoryStream(e.RawData))
					Received.Invoke(ms);
			}

			public void Dispose()
			{
				_socket.OnMessage -= OnMessageReceived;
			}

			public Task Send(Stream stream)
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

		public static T RegisterInterfaceAsProxy<T>(this WebSocket socket, ICommonSerializer serializer) where T : class
		{
			return RegisterInterfaceAsProxy<T>(socket, serializer, new RoslynProxyObjectGenerator());
		}


		public static T RegisterInterfaceAsProxy<T>(this WebSocket socket, ICommonSerializer serializer, IProxyObjectGenerator generator) where T : class
		{
			return generator.Create<T>(new WebSocketSharpComonizer(socket),  serializer);
		}

		public static void RegisterService<T>(T service, ICommonSerializer serializer)

	}
}
