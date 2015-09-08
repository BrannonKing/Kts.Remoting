using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kts.Remoting.Client
{
	public class ProxyClient<T> : IDisposable
	{
		private readonly Uri _address;
		private readonly NetworkCredential _credentials;
		private ClientWebSocket _client;

		static ProxyClient()
		{
			var type = typeof(T);
			if (!type.IsInterface)
				throw new NotSupportedException("The type T is required to be an interface. T = " + type);

			var methods = type.GetMethods();
			foreach (var method in methods)
				if (!typeof(Task).IsAssignableFrom(method.ReturnType))
					throw new NotSupportedException("Methods are required to return type System.Threading.Tasks.Task<...>. Method = " + method.Name);

			var actions = type.GetEvents();
			foreach (var action in actions)
				if (action.EventHandlerType.GetMethod("Invoke").ReturnType != typeof(void))
					throw new NotSupportedException("Events are required to not have return types. Event = " + action.Name);

			var properties = type.GetProperties();
			if (properties.Any() && !typeof(INotifyPropertyChanged).IsAssignableFrom(type))
				throw new NotSupportedException("Properties are not supported without the INotifyPropertyChanged as a base interface.");

		}

		public ProxyClient(string address, NetworkCredential credentials)
		{
			_address = new Uri(address);
			_credentials = credentials;
		}

		public Task Connect()
		{
			_client = new ClientWebSocket();
			_client.Options.Credentials = _credentials;
			return _client.ConnectAsync(_address, CancellationToken.None);
		}

		public void Dispose()
		{
			if (_client != null)
				_client.Dispose();
		}
	}
}
