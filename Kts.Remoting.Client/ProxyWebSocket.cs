using System;
using System.IO;
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
			_client.ReceiveAsync()
		}

		public void Dispose()
		{
			if (_client != null)
				_client.Dispose();
		}

		private static readonly CancellationToken _cancellationToken = CancellationToken.None;
		public async Task Send(Stream bytes, bool binary)
		{
			if (_client.State != WebSocketState.Open)
				await Connect();

			// a few options: 
			// 1. allocate a small array and do multiple sends
			// 2. allocate a medium array and do multiple sends
			// 3. allocate all we need to hold the stream and do a single send
			// 4. pull a medium buffer from some pool of buffers and allocate one if they're all busy; push it back on when done

			// TODO: allow an option to hard-limit the message size. (Some people like that as a security feature.)

			while (bytes.r) ;
			await _client.SendAsync(bytes, binary ? WebSocketMessageType.Binary : WebSocketMessageType.Text, true, _cancellationToken);
		}

		public event Action<ArraySegment<byte>> Received;
	}
}
