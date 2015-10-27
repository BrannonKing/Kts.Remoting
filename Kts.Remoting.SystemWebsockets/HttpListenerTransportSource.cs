using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public class HttpListenerTransportSource : ITransportSource
	{
		private readonly HttpListener _listener;
		private readonly ConcurrentDictionary<WebSocket, WebSocketTransportSource> _cache = new ConcurrentDictionary<WebSocket, WebSocketTransportSource>();

		public HttpListenerTransportSource(HttpListener listener)
		{
			_listener = listener;
			StartListening();
		}

		private async void StartListening()
		{
			do // TODO: add the appropriate try/catch here
			{
				var context = await _listener.GetContextAsync();
				if (context != null && context.Request.IsWebSocketRequest && !_disposed)
					StartAccepting(context);
				else if (context != null && !context.Request.IsWebSocketRequest)
				{
					context.Response.StatusCode = 400;
					context.Response.Close();
				}
			} while (_listener.IsListening && !_disposed);
		}

		private async void StartAccepting(HttpListenerContext context)
		{
			do // TODO: add the appropriate try/catch here, utilize User
			{
				try
				{
					var socketContext = await context.AcceptWebSocketAsync(null);
					if (socketContext != null && !_disposed)
					{
						var child = new WebSocketTransportSource(socketContext.WebSocket);
						child.Received += OnChildReceived;
						_cache[socketContext.WebSocket] = child;
					}
				}
				catch (IndexOutOfRangeException)
				{
					// no idea what is causing this
					break;
				}

			} while (_listener.IsListening && !_disposed);
		}

		private void OnChildReceived(object sender, DataReceivedArgs e)
		{
			Received.Invoke(this, e);
		}

		private bool _disposed;

		public void Dispose()
		{
			_disposed = true;
			foreach(var child in _cache.Values)
				child.Received -= OnChildReceived;
			_cache.Clear();
		}

		public async Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			var tasks = new List<Task>(connectionIDs.Length);
			foreach (WebSocket ws in connectionIDs)
				tasks.Add(_cache[ws].Send(data, ws));
			await Task.WhenAll(tasks);
		}

		public event EventHandler<DataReceivedArgs> Received = delegate{};
	}
}