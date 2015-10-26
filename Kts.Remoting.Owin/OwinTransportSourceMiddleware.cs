using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;
using AppFunc = System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	#region Websocket Method Definitions

	using WebSocketAccept =
		Action
		<
			IDictionary<string, object>, // WebSocket Accept parameters
			Func // WebSocketFunc callback
			<
				IDictionary<string, object>, // WebSocket environment
				Task // Complete
			>
		>;

	using WebSocketFunc =
		Func
		<
			IDictionary<string, object>, // WebSocket Environment
			Task // Complete
		>;

	using WebSocketSendAsync =
		Func
		<
			ArraySegment<byte> /* data */,
			int /* messageType */,
			bool /* endOfMessage */,
			CancellationToken /* cancel */,
			Task
		>;

	using WebSocketReceiveAsync =
		Func
		<
			ArraySegment<byte> /* data */,
			CancellationToken /* cancel */,
			Task
			<
				Tuple
				<
					int /* messageType */,
					bool /* endOfMessage */,
					int /* count */
				>
			>
		>;

	using WebSocketReceiveTuple =
		Tuple
		<
			int /* messageType */,
			bool /* endOfMessage */,
			int /* count */
		>;

	using WebSocketCloseAsync =
		Func
		<
			int /* closeStatus */,
			string /* closeDescription */,
			CancellationToken /* cancel */,
			Task
		>;

	#endregion

	public class OwinTransportSourceMiddleware : ITransportSource
	{
		public OwinTransportSourceMiddleware()
		{
			CancellationToken = CancellationToken.None;
		}

		public CancellationToken CancellationToken { get; set; }

		public void Invoke(IOwinContext context)
		{
			var accept = context.Get<WebSocketAccept>("websocket.Accept");
			if (accept == null)
			{
				// Bad Request
				context.Response.StatusCode = 400;
				context.Response.Write("Not a valid websocket request");
				return;
			}

			var responseBuffering = context.Get<Action>("server.DisableResponseBuffering");
			if (responseBuffering != null)
				responseBuffering.Invoke();

			var responseCompression = context.Get<Action>("systemweb.DisableResponseCompression");
			if (responseCompression != null)
				responseCompression.Invoke();

			context.Response.Headers.Set("X-Content-Type-Options", "nosniff");

			accept.Invoke(null, RunReadLoop);
		}

		private async Task RunReadLoop(IDictionary<string, object> websocketContext)
		{
			// can't get these before the accept call, apparently
			var sendAsync = (WebSocketSendAsync)websocketContext["websocket.SendAsync"];
			var receiveAsync = (WebSocketReceiveAsync)websocketContext["websocket.ReceiveAsync"];
			var closeAsync = (WebSocketCloseAsync)websocketContext["websocket.CloseAsync"];

			var buffer = new byte[8192];
			var segment = new ArraySegment<byte>(buffer, 0, buffer.Length);

			var stream = new MemoryStream();

			// make sure thread username gets propagated to the handler thread
			// should we have a connectionID -- some random number here? maybe it's in the context?
			// connected and disconnected need try/catch
			// disconnected may need the reason
			do
			{
				try
				{
					WebSocketReceiveTuple received;
					stream.SetLength(0);
					do
					{
						received = await receiveAsync.Invoke(segment, CancellationToken);
						stream.Write(segment.Array, segment.Offset, received.Item3);
					} while (!received.Item2 && !CancellationToken.IsCancellationRequested);

					if (CancellationToken.IsCancellationRequested)
						break;

					var isClosed = (received.Item1 & CLOSE_OP) > 0;
					if (isClosed)
						break;

					var isUTF8 = (received.Item1 & TEXT_OP) > 0;
					var isCompressed = (received.Item1 & 0x40) > 0;

					var args = new DataReceivedArgs { SessionID = sendAsync };
					if (isCompressed)
					{
						stream.Position = 0;
						var array = Decompress(stream);
						args.Data = new ArraySegment<byte>(array, 0, array.Length);
					}
					else
					{
						args.Data = new ArraySegment<byte>(stream.GetBuffer(), 0, (int) stream.Length);
					}

					Received.Invoke(this, args);
				}
				catch (TaskCanceledException)
				{
					closeAsync.Invoke(0, "Cancellation Token Triggered", CancellationToken.None);
					break;
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (ObjectDisposedException)
				{
					break;
				}
				catch (Exception ex)
				{
					if (IsFatalSocketException(ex))
					{
						throw;
					}
					break;
				}
			}
			while (true);
			stream.Dispose();
		}

		private static byte[] Compress(Stream input)
		{
			using (var compressStream = new MemoryStream())
			using (var compressor = new DeflateStream(compressStream, CompressionMode.Compress))
			{
				input.CopyTo(compressor);
				compressor.Close();
				return compressStream.ToArray();
			}
		}

		private static byte[] Decompress(Stream input)
		{
			using (var compressStream = new MemoryStream())
			using (var compressor = new DeflateStream(compressStream, CompressionMode.Decompress))
			{
				input.CopyTo(compressor);
				compressor.Close();
				return compressStream.ToArray(); // TODO: we don't need to copy the array out
			}
		}

		internal const int TEXT_OP = 0x1;
		internal const int BINARY_OP = 0x2;
		internal const int CLOSE_OP = 0x8;
		internal const int PONG = 0xA;

		internal static bool IsFatalSocketException(Exception ex)
		{
			// If this exception is due to the underlying TCP connection going away, treat as a normal close
			// rather than a fatal exception.
			var ce = ex as COMException;
			if (ce != null)
			{
				switch ((uint)ce.ErrorCode)
				{
					case 0x800703e3:
					case 0x800704cd:
					case 0x80070026:
						return false;
				}
			}

			// unknown exception; treat as fatal
			return true;
		}

		public void Dispose()
		{
		}

		public async Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			foreach (WebSocketSendAsync sender in connectionIDs)
			{
				await sender.Invoke(data, BINARY_OP, true, CancellationToken);
			}
		}

		public event EventHandler<DataReceivedArgs> Received = delegate { };
	}
}

namespace Owin
{
	using Kts.Remoting.Shared;

	public static class OwinExtension
	{
		public static ITransportSource GenerateTransportSource(this IAppBuilder app, string route)
		{
			var source = new OwinTransportSourceMiddleware();
			app.Map(route, app2 =>
			{
				app2.Run(context =>
				{
					source.Invoke(context);
					return Task.FromResult(true);
				});
			});
			return source;
		}
	}
}
