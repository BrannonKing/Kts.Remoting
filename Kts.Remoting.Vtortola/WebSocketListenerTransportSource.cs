using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IO;
using vtortola.WebSockets;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public class WebSocketListenerTransportSource : ITransportSource
	{
		private readonly WebSocketListener _listener;
		private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
		private readonly ConcurrentQueue<WebSocket> _sockets = new ConcurrentQueue<WebSocket>();

		public WebSocketListenerTransportSource(WebSocketListener listener)
		{
			_listener = listener;
			ListenForSockets();
		}

		private async void ListenForSockets()
		{
			TOP:
			try
			{
				var socket = await _listener.AcceptWebSocketAsync(_tokenSource.Token);
				if (socket != null)
				{
					_sockets.Enqueue(socket);
					ListenForMessages(socket);
					goto TOP;
				}
			}
			catch (TaskCanceledException) { }
			catch (Exception)
			{
				goto TOP;
			}
		}

		private static readonly RecyclableMemoryStreamManager _streamManager = new RecyclableMemoryStreamManager();

		private async void ListenForMessages(WebSocket socket)
		{
			TOP:
			try
			{
				var message = await socket.ReadMessageAsync(_tokenSource.Token);
				if (message != null)
				{
					using (var ms = _streamManager.GetStream("VtortolaReceived")) // length was throwing an exception
					{
						message.CopyTo(ms);
						var segment = new ArraySegment<byte>(ms.GetBuffer(), 0, (int)ms.Length);
						var args = new DataReceivedArgs { Data = segment, SessionID = socket };
						Received.Invoke(this, args);
					}
				}
			}
			catch (TaskCanceledException) { }
			catch (Exception)
			{
				goto TOP;
			}
		}

		public void Dispose()
		{
			_tokenSource.Cancel();
			WebSocket socket;
			while (_sockets.TryDequeue(out socket))
				socket.Dispose();
		}

		public async Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			var tasks = new List<Task>(connectionIDs.Length);
			foreach (WebSocket socket in connectionIDs)
			{
				if (!socket.IsConnected) continue;
				using (var writer = socket.CreateMessageWriter(WebSocketMessageType.Binary))
					tasks.Add(writer.WriteAsync(data.Array, data.Offset, data.Count));
			}
			await Task.WhenAll(tasks);
		}

		public event EventHandler<DataReceivedArgs> Received = delegate { };
	}
}