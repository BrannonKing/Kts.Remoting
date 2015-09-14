using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer;

namespace Kts.Remoting.Client
{
	class ProxyBase: IDisposable
	{
		private readonly ICommonWebSocket _socket;
		protected readonly ICommonSerializer _serializer;
		private readonly ConcurrentDictionary<Message, dynamic> _sentMessages = new ConcurrentDictionary<Message, dynamic>();
		private readonly string _hubName;

		protected ProxyBase(ICommonWebSocket socket, ICommonSerializer serializer, string hubName)
		{
			_hubName = hubName;
			_socket = socket;
			_serializer = serializer;
			_socket.Received += OnReceived;
		}

		private static long _counter = DateTime.UtcNow.Ticks;

		protected async Task<T> Send<T>(Message message)
		{
			var source = new TaskCompletionSource<T>();
			message.Hub = _hubName;
			do
			{
				message.ID = ToBase62(Interlocked.Increment(ref _counter));
			} while (!_sentMessages.TryAdd(message, source));

			using (var ms = new MemoryStream()) // TODO: make a buffer pool
			{
				_serializer.Serialize(message, ms);
				ms.Position = 0;
				await _socket.Send(ms, !_serializer.StreamsUtf8);
			}
			return await source.Task;
		}

		private static readonly char[] Base62Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

		protected static string ToBase62(long value)
		{
			var str = "";
			var len = (long)Base62Chars.Length;
			while (value != 0)
			{
				var rem = value % len;
				str += Base62Chars[rem];
				value /= len;
			}
			return str;
		}

		public virtual void Dispose()
		{
			_socket.Received -= OnReceived;
		}

		private void OnReceived(Stream stream)
		{
			var message = _serializer.Deserialize<Message>(stream);

			dynamic source;
			if (_sentMessages.TryRemove(message, out source))
			{
				if (!string.IsNullOrEmpty(message.Error))
					source.SetException(new Exception(message.Error) {Data = {{"Far Stack Trace", message.StackTrace}}});
				else
					FillSource(source, message);
			}
		}

		private void FillSource<T>(TaskCompletionSource<T> source, Message message)
		{
			if (message.Results == null)
				source.SetResult(default(T));
			else
			{
				var result = _serializer.Deserialize<T>(message.Results);
				source.SetResult(result);
			}
		}
	}
}
