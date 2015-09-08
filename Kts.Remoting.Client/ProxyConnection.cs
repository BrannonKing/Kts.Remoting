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
	public class ProxyClient : IDisposable
	{
		private readonly Uri _address;
		private readonly NetworkCredential _credentials;
		private ClientWebSocket _client;

		public ProxyClient(string address, NetworkCredential credentials = null)
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
