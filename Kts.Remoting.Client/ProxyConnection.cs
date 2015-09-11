using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Kts.Remoting.Client
{
	public class ProxyWebSocket : IProxyWebSocket
	{
		private readonly Uri _address;
		private readonly NetworkCredential _credentials;
		private ClientWebSocket _client;

		public ProxyWebSocket(string address, NetworkCredential credentials = null)
		{
			_address = new Uri(address);
			_credentials = credentials;
		}

		public async Task Connect()
		{
			_client = new ClientWebSocket();
			_client.Options.Credentials = _credentials;
			await _client.ConnectAsync(_address, CancellationToken.None);
			_client.ReceiveAsync().Result
		}

		public void Dispose()
		{
			if (_client != null)
				_client.Dispose();
		}

		private static readonly CancellationToken _cancellationToken = CancellationToken.None;
		public async Task Send(ArraySegment<byte> bytes, bool binary)
		{
			if (_client.State != WebSocketState.Open)
				await Connect();
			await _client.SendAsync(bytes, binary ? WebSocketMessageType.Binary : WebSocketMessageType.Text, true, _cancellationToken);
		}

		public event Action<ArraySegment<byte>> Received;
	}
}
