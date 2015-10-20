using System;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocket = WebSocketSharp.WebSocket;

namespace Kts.Remoting.WebSocketSharp
{
	public static class WebSocketExtensions
	{
		private class ServerWebSocketSharpTransport : WebSocketSharpTransport
		{
			private readonly ServiceRegistrationOptions _options;

			public ServerWebSocketSharpTransport(WebSocket socket, ServiceRegistrationOptions options)
				: base(socket)
			{
				_options = options;
			}
		}

		private class ClientWebSocketSharpTransport : WebSocketSharpTransport
		{
			private readonly InterfaceRegistrationOptions _options;

			public ClientWebSocketSharpTransport(WebSocket socket, InterfaceRegistrationOptions options)
				: base(socket)
			{
				_options = options;
				if (options.CompressSentMessages)
					socket.Compression = CompressionMethod.Deflate;

				socket.OnOpen += SocketOnOnOpen;
				socket.OnClose += SocketOnOnClose;
			}

			public override void Dispose()
			{
				_socket.OnOpen -= SocketOnOnOpen;
				_socket.OnClose -= SocketOnOnClose;

				base.Dispose();
			}

			private void SocketOnOnClose(object sender, CloseEventArgs closeEventArgs)
			{
				_options.FireOnDisconnected();
			}

			private void SocketOnOnOpen(object sender, EventArgs eventArgs)
			{
				_options.FireOnConnected();
			}
		}

		private class WebSocketSharpTransport : ICommonTransport
		{
			protected readonly WebSocket _socket;

			public WebSocketSharpTransport(WebSocket socket)
			{
				_socket = socket;
				_socket.OnMessage += OnMessageReceived;
			}

			private void OnMessageReceived(object sender, MessageEventArgs e)
			{
				var args = new DataReceivedArgs { Data = e.RawData, DataCount = e.RawData.Length };
				Received.Invoke(this, args);
			}

			public virtual void Dispose()
			{
				_socket.OnMessage -= OnMessageReceived;
			}

			public Task Send(DataToSendArgs args)
			{
				var source = new TaskCompletionSource<bool>();
				_socket.SendAsync(args.Data, success =>
				{
					if (success)
						source.SetResult(true);
					else
						source.SetException(new Exception("Unable to send all the data."));
				});
				return source.Task;
			}

			public event EventHandler<DataReceivedArgs> Received = delegate { };
		}

		public static T RegisterInterface<T>(this WebSocket socket, InterfaceRegistrationOptions options) where T : class
		{
			return options.Generator.Create<T>(new ClientWebSocketSharpTransport(socket, options), options.Serializer, options.ServiceName);
		}

		public static void RegisterServices(this WebSocket socket, ServiceRegistrationOptions options)
		{
			var transport = new ServerWebSocketSharpTransport(socket, options);
			foreach (var service in options.Services)
				options.Generator.Create(transport, options.Serializer, service.Value, service.Key);
		}

	}
}
