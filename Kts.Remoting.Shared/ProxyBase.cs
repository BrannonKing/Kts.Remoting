using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer;

namespace Kts.Remoting.Shared
{
	public abstract class ProxyBase: IMessageHandler
	{
		protected readonly ICommonSerializer _serializer;
		private readonly ConcurrentDictionary<Message, dynamic> _sentMessages = new ConcurrentDictionary<Message, dynamic>();
		private readonly IMessageHandler _handler;
		private readonly string _hubName;

		protected ProxyBase(IMessageHandler handler, ICommonSerializer serializer, string hubName)
		{
			_handler = handler;
			_hubName = hubName;
			_serializer = serializer;
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

			await _handler.Handle(type => message).ConfigureAwait(false);
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

		public Task Handle(Func<Type, Message> getOrCreateMessage)
		{
			dynamic source;
			var message = getOrCreateMessage.Invoke(null);
			if (_sentMessages.TryRemove(message, out source))
			{
				FillSource(source, getOrCreateMessage); // TODO: eliminate DLR call
			}
			return Task.FromResult(true);
		}

		private void FillSource<T>(TaskCompletionSource<T> source, Func<Type, Message> generator)
		{
			var message = (ResponseMessage<T>)generator.Invoke(typeof(ResponseMessage<T>));
			if (!string.IsNullOrEmpty(message.Error))
				source.SetException(new Exception(message.Error) { Data = { { "Far Stack Trace", message.StackTrace } } });
			else
				source.SetResult(message.Results);
		}
	}
}
