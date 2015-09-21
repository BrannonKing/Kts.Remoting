using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Owin;
using CommonSerializer;
using Microsoft.Owin;

namespace Kts.Remoting.Server
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

	public class ProxyMiddleware : OwinMiddleware
	{
		private readonly OptionsForProxiedServices _options;

		public ProxyMiddleware(OwinMiddleware next, OptionsForProxiedServices options)
			: base(next)
		{
			_options = options;
		}

		public override async Task Invoke(IOwinContext context)
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
			_options.FireOnConnected();

			var sendAsync = (WebSocketSendAsync)websocketContext["websocket.SendAsync"];
			var receiveAsync = (WebSocketReceiveAsync)websocketContext["websocket.ReceiveAsync"];
			var closeAsync = (WebSocketCloseAsync)websocketContext["websocket.CloseAsync"];


			foreach (var kvp in _options.Services)
			{
				// subscribe to events
				// if it's a PropertyChanged event, send the property data

			}

			var buffer = new byte[4096];

			Stream stream = new MemoryStream();

			// make sure thread username gets propagated to the handler thread
			// should we have a connectionID -- some random number here? maybe it's in the context?
			// connected and disconnected need try/catch
			// disconnected may need the reason
			do
			{
				try
				{
					if (_options.CancellationToken.IsCancellationRequested)
						break;

					WebSocketReceiveTuple received;
					stream.SetLength(0);
					do
					{
						var segment = new ArraySegment<byte>(buffer, 0, buffer.Length);
						received = await receiveAsync.Invoke(segment, _options.CancellationToken);
						stream.Write(segment.Array, segment.Offset, received.Item3);
					} while (!received.Item2 && !_options.CancellationToken.IsCancellationRequested);

					if (_options.CancellationToken.IsCancellationRequested)
						break;

					var isClosed = (received.Item1 & 0x8) > 0;
					if (isClosed)
						break;

					var isUTF8 = (received.Item1 & 0x01) > 0;
					var isCompressed = (received.Item1 & 0x40) > 0;

					// decompress the thing
					// deserialize the thing
					var serializer = _options.Serializer;

					Message message;
					try
					{
						if (isCompressed)
							stream = new DeflateStream(stream, CompressionMode.Decompress, false);

						message = serializer.Deserialize<Message>(stream);
					}
					finally
					{
						stream.Dispose();
					}

					// options for deserializing the parameters:
					// 1. find the method based on name and count. Overloads with the same parameter count will be disallowed.
					// 2. send the parameters in an ordered dictionary by name. Overloads with conflicting names would be disallowed.
					// 3. use the method name alone: overloads would be disallowed.
					// 4. attempt to deserialize the parameters without passing in any type information
					// 5. #1 plus some attempt at extracting string-typed items before method resolution

					var service = _options.Services[message.Hub];
					var method = GetMethodDelegateFromCache(service, message);
					
					try
					{
						if (method == null)
							_options.FireOnError(new MissingMethodException(message.Hub, message.Method));
						else
						{
							var parameters = method.GetParameters();
							var arguments = new object[parameters.Length];
							for (int i = 0; i < parameters.Length; i++)
								arguments[i] = serializer.Deserialize(message.Arguments, parameters[i].ParameterType);
							var ret = method.Invoke(service, arguments);
							if (ret is Task)
								await (Task)ret;
						}
					}
					catch (Exception ex)
					{
						// any exception here should not be treated as a shutdown request
						_options.FireOnError(ex);
					}
				}
				catch (TaskCanceledException)
				{
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
						_options.FireOnError(ex);
					}
					break;
				}
			}
			while (true);

			_options.FireOnDisconnected();
		}


		private static readonly ConcurrentDictionary<Tuple<string, string, int>, MethodInfo> _methodCache = new ConcurrentDictionary<Tuple<string, string, int>, MethodInfo>();
		private MethodInfo GetMethodDelegateFromCache(object hub, Message message)
		{
			var count = message.Arguments != null ? message.Arguments.Count : 0;
			var key = Tuple.Create(message.Hub, message.Method, count);
			return _methodCache.GetOrAdd(key, id =>
			{
				var methods = hub.GetType().GetMethods().Where(m => string.Equals(m.Name, message.Method, StringComparison.OrdinalIgnoreCase)).ToList();
				if (methods.Count > 1)
				{
					// filter by parameters
					methods = methods.Where(m => m.GetParameters().Length == count).ToList();
				}

				if (methods.Count <= 0 && count == 1)
				{
					var property = hub.GetType().GetProperty(message.Method, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
					if (property != null)
					{
						return property.GetSetMethod();
					}
				}

				if (methods.Count != 1)
					return null;

				return methods[0];
			});
		}

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

	}
}

namespace Owin
{
	using Kts.Remoting.Server;

	public class OptionsForProxiedServices
	{
		internal Dictionary<string, object> Services = new Dictionary<string, object>();
		private CancellationToken _cancellationToken = CancellationToken.None;

		public void AddService<T>(T service)
		{
			AddService(service, typeof(T).Name);
		}

		public void AddService<T>(T service, string name)
		{
			if (service == null)
				throw new ArgumentNullException("service");
			if (string.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");

			Services.Add(name, service);
		}

		/// <summary>
		/// Use this to shutdown all the client connections.
		/// </summary>
		public CancellationToken CancellationToken
		{
			get { return _cancellationToken; }
			set { _cancellationToken = value; }
		}

		/// <summary>
		/// Use the deflate algorithm when sending data to the client.
		/// </summary>
		public bool CompressSentMessages { get; set; }

		public ICommonSerializer Serializer { get; set; }

		/// <summary>
		/// By default, all exceptions are eaten. Subscribe here to see or do something with them.
		/// </summary>
		public event Action<Exception> OnError = delegate { };
		internal void FireOnError(Exception ex) { OnError.Invoke(ex); }

		/// <summary>
		/// Triggers after each client successfully conencts.
		/// </summary>
		public event Action OnConnected = delegate { };
		internal void FireOnConnected() { OnConnected.Invoke(); }

		/// <summary>
		/// Triggers after a client disconnect, be it requested or due to an exception.
		/// </summary>
		public event Action OnDisconnected = delegate { };
		internal void FireOnDisconnected() { OnDisconnected.Invoke(); }
	}

	public static class OwinExtension
	{
		public static void AddProxiedServices(this IAppBuilder app, string route, OptionsForProxiedServices options)
		{
			app.Map(route, config => config.Use<ProxyMiddleware>(options));
		}
	}
}
