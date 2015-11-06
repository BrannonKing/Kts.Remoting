using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using CommonSerializer;

namespace Kts.Remoting.Shared
{
	public class DefaultMessageRouter: IMessageRouter
	{
		private readonly ConcurrentDictionary<string, IMessageHandler> _handlers = new ConcurrentDictionary<string, IMessageHandler>();

		public DefaultMessageRouter(ITransportSource transportSource, ICommonSerializer serializer, 
			IProxyObjectGenerator proxyObjectGenerator = null, IServiceWrapperGenerator serviceWrapperGenerator = null)
		{
			TransportSource = transportSource;
			TransportSource.Received += TransportSourceOnReceived;
			Serializer = serializer;
			ProxyObjectGenerator = proxyObjectGenerator ?? new DefaultProxyObjectGenerator();
			ServiceWrapperGenerator = serviceWrapperGenerator ?? new DefaultServiceWrapperGenerator();
		}

		private void TransportSourceOnReceived(object sender, DataReceivedArgs args)
		{
			using (var stream = new MemoryStream(args.Data.Array, args.Data.Offset, args.Data.Count, false))
			{
				var message = Serializer.Deserialize<Message>(stream);
				message.SessionID = args.SessionID;
				var target = _handlers[message.Hub];
				target.Handle(type => // expecting synchronous usage
				{
					if (type == null || type.IsInstanceOfType(message)) 
						return message;
					stream.Position = 0;
					var ret = (Message)Serializer.Deserialize(stream, type);
					ret.SessionID = args.SessionID;
					return ret;
				});
			}
		}

		public void Dispose()
		{
			TransportSource.Received -= TransportSourceOnReceived;
			_handlers.Clear();
		}

		public T AddInterface<T>(string nameOverride = null) where T:class
		{
			var name = nameOverride ?? typeof(T).Name;
			var proxy = ProxyObjectGenerator.Create<T>(this, Serializer, name);
			if (!_handlers.TryAdd(name, (IMessageHandler) proxy))
				throw new ArgumentException("Service name is already in use.");
			return proxy;
		}

		public void AddService<T>(T service, string nameOverride = null)
		{
			var name = nameOverride ?? typeof(T).Name;
			var wrapper = ServiceWrapperGenerator.Create(this, Serializer, service);
			if (!_handlers.TryAdd(name, wrapper))
				throw new ArgumentException("Service name is already in use.");
		}

		public ICommonSerializer Serializer { get; private set; }
		public ITransportSource TransportSource { get; private set; }
		public IProxyObjectGenerator ProxyObjectGenerator { get; private set; }
		public IServiceWrapperGenerator ServiceWrapperGenerator { get; private set; }

		public async Task Handle(Func<Type, Message> getOrCreateMessage)
		{
			var message = getOrCreateMessage.Invoke(null);
			using (var stream = new MemoryStream())
			{
				Serializer.Serialize(stream, message, message.GetType());
				var segment = new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length);
				await TransportSource.Send(segment, message.SessionID).ConfigureAwait(false);
			}
		}
	}
}
