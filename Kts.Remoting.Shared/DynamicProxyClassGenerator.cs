using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonSerializer;
using Castle.DynamicProxy;
using System.ComponentModel;
using System.Threading;
using System.IO;
using System.Collections.Concurrent;

namespace Kts.Remoting.Client
{
	public class DynamicProxyClassGenerator : IProxyClassGenerator
	{
		private readonly ProxyGenerator _generator = new ProxyGenerator();

		public T Create<T>(ICommonWebSocket socket, ICommonSerializer serializer) where T : class
		{
			ValidateInterface(typeof(T));
			return _generator.CreateInterfaceProxyWithoutTarget<T>(new InterfaceInterceptor(socket, serializer, typeof(T).Name));
		}

		private void ValidateInterface(Type type)
		{
			if (!type.IsInterface)
				throw new ArgumentException("An interface type is required.");

			var methods = type.GetMethods();
			foreach (var method in methods)
				if (!typeof(Task).IsAssignableFrom(method.ReturnType))
					throw new NotSupportedException("Methods are required to return type System.Threading.Tasks.Task<...>. Invalid method: " + method.Name);

			var actions = type.GetEvents();
			foreach (var action in actions)
				if (action.EventHandlerType.GetMethod("Invoke").ReturnType != typeof(void))
					throw new NotSupportedException("Events are required to not have return types. Invalid event: " + action.Name);

			var properties = type.GetProperties();
			if (properties.Any() && !typeof(INotifyPropertyChanged).IsAssignableFrom(type))
				throw new NotSupportedException("Property use requires INotifyPropertyChanged as a base interface.");
		}

		private class InterfaceInterceptor : IInterceptor
		{
			private readonly ICommonSerializer _serializer;
			private readonly ICommonWebSocket _socket;
			private readonly ConcurrentDictionary<Message, dynamic> _sentMessages = new ConcurrentDictionary<Message, dynamic>();
			private readonly string _hubName;
			private static long _counter = DateTime.UtcNow.Ticks;

			public InterfaceInterceptor(ICommonWebSocket socket, ICommonSerializer serializer, string hubName)
			{
				_socket = socket;
				_serializer = serializer;
				_hubName = hubName;

				_socket.Received += OnReceived;
			}

			public void Intercept(IInvocation invocation)
			{
				var message = new Message();
				message.Method = invocation.Method.Name;
				message.Arguments = _serializer.GenerateContainer();
				var parameters = invocation.GetConcreteMethod().GetParameters();
				for(int i = 0; i < invocation.Arguments.Length; i++)
					_serializer.Serialize(message.Arguments, invocation.Arguments[i], parameters[i].ParameterType);

				// left off: need serializer overloads that take type. 
				// Also, add multiples simultaneously to the container

				invocation.ReturnValue = Send(message, invocation.Method.ReturnType);
			}

			private object Send(Message message, Type returnType)
			{
				var genericReturnTypes = returnType.GetGenericArguments();
				if (genericReturnTypes.Length > 1)
					throw new NotSupportedException("Expecting a single generic type on a Task return type.");

				var genericType = genericReturnTypes.Length > 0 ? genericReturnTypes[0] : typeof(bool);

				var sourceType = typeof(TaskCompletionSource<>).MakeGenericType(genericType);
				dynamic source = Activator.CreateInstance(sourceType);
				message.Hub = _hubName;
				do
				{
					message.ID = ToBase62(Interlocked.Increment(ref _counter));
				} while (!_sentMessages.TryAdd(message, source));

				var ms = new MemoryStream(); // TODO: make a buffer pool
				_serializer.Serialize(ms, message);
				ms.Position = 0;
				// TODO: do something with errors/faulted state in this ContinueWidth
				_socket.Send(ms, !_serializer.StreamsUtf8).ContinueWith(prev => ms.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
				return source.Task;
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

			private void OnReceived(Stream stream)
			{
				var message = _serializer.Deserialize<Message>(stream);

				dynamic source;
				if (_sentMessages.TryRemove(message, out source))
		   		{
					if (!string.IsNullOrEmpty(message.Error))
						source.SetException(new Exception(message.Error) { Data = { { "Far Stack Trace", message.StackTrace } } });
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
}
