using System;
using System.Threading.Tasks;
using Udt;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public static class UdtExtensions
	{
		public static ITransportSource GenerateTransportSource(this Socket socket, bool isServer)
		{
			return new UdtSocketTransportSource(socket, isServer);
		}
	}

	public class UdtSocketTransportSource : ITransportSource
	{
		private readonly Socket _socket;

		public UdtSocketTransportSource(Socket socket, bool isServer)
		{
			_socket = socket;

			if (isServer)
			{
				StartAcceptingConnections(socket);
			}
			else
				StartReceivingMessages(socket, new byte[socket.ReceiveBufferSize]);
		}

		private void StartAcceptingConnections(Socket socket)
		{
			var task = new Task(() =>
			{
				while (!socket.IsDisposed)
				{
					var connection = socket.Accept();
					if (connection != null)
						StartReceivingMessages(connection, new byte[socket.ReceiveBufferSize]);
				}
			}, TaskCreationOptions.LongRunning);
			task.Start();
		}

		private void StartReceivingMessages(Socket socket, byte[] buffer)
		{
			var task = new Task(() =>
			{
				while (!socket.IsDisposed)
				{
					var received = socket.ReceiveMessage(buffer);
					if (received >= 0)
					{
						var args = new DataReceivedArgs
						{
							Data = new ArraySegment<byte>(buffer, 0, received),
							SessionID = socket
						};
						Received.Invoke(this, args);
					}
				}
			}, TaskCreationOptions.LongRunning);
			task.Start();
		}

		public void Dispose()
		{
			
		}

		public Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			if (connectionIDs == null || connectionIDs.Length <= 0)
				_socket.SendMessage(data.Array, data.Offset, data.Count);
			else
			{
				for (int i = 0; i < connectionIDs.Length; i++)
				{
					var socket = connectionIDs[i] == null ? _socket : (Socket)connectionIDs[i];
					socket.SendMessage(data.Array, data.Offset, data.Count);
				}
			}
			return Task.FromResult(true);

		}

		public event EventHandler<DataReceivedArgs> Received = delegate{};
	}
}
