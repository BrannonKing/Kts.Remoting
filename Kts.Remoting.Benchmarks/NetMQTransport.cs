using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using WampSharp.Core.Listener;
using WampSharp.Core.Message;
using WampSharp.V2.Binding;
using WampSharp.V2.Transports;

namespace Kts.Remoting.Benchmarks
{
	// Only used on the server:
	public class NetMQTransport: TextBinaryTransport<NetMQSocket>
	{
		private readonly NetMQSocket _socket;
		private readonly NetMQScheduler _scheduler;

		public NetMQTransport(NetMQSocket socket, NetMQScheduler scheduler)
		{
			_socket = socket;
			_scheduler = scheduler;
		}

		public override void Dispose()
		{
		}

		protected override void OpenConnection<TMessage>(IWampConnection<TMessage> connection)
		{
			throw new NotSupportedException();
		}

		protected override string GetSubProtocol(NetMQSocket connection)
		{
			return "";
		}

		protected override IWampConnection<TMessage> CreateBinaryConnection<TMessage>(NetMQSocket connection, IWampBinaryBinding<TMessage> binding)
		{
			return new BinaryNetMQConnection<TMessage>(binding, _socket, _scheduler);
		}

		protected override IWampConnection<TMessage> CreateTextConnection<TMessage>(NetMQSocket connection, IWampTextBinding<TMessage> binding)
		{
			throw new NotImplementedException();
		}

		public override void Open()
		{
			// need to bind to start listener
			_socket.Bind()
		}
	}

	public class BinaryNetMQConnection<TMessage> : AsyncWampConnection<TMessage>, IControlledWampConnection<TMessage>
	{
		private readonly IWampBinaryBinding<TMessage> _binding;
		private readonly NetMQSocket _socket;
		private readonly NetMQScheduler _scheduler;

		public BinaryNetMQConnection(IWampBinaryBinding<TMessage> binding, NetMQSocket socket, NetMQScheduler scheduler)
		{
			_binding = binding;
			_socket = socket;
			_scheduler = scheduler;
			_socket.ReceiveReady += OnReceiveMessage;
		}

		private void OnReceiveMessage(object sender, NetMQSocketEventArgs e)
		{
			var message = e.Socket.ReceiveMultipartMessage();
			var connectionId = e.Socket is RouterSocket && message.FrameCount > 1 ? message.First.ToByteArray() : null;
			using (var ms = new MemoryStream(message.Last.Buffer, 0, message.Last.MessageSize, false))
				RaiseMessageArrived(_binding.Parse(ms));
		}

		protected override Task SendAsync(WampMessage<object> message)
		{
			var task = new Task(() =>
			{
				var msg = new NetMQMessage();
				if (_socket is RouterSocket)
				{
					msg.Append(new byte[0]);
					msg.AppendEmptyFrame();
				}

				var bytes = _binding.Format(message);
				msg.Append(bytes);
				_socket.SendMultipartMessage(msg);
			});
			task.Start(_scheduler);
			return task;
		}

		public void Connect()
		{
		}

		protected override void Dispose()
		{
			_socket.ReceiveReady -= OnReceiveMessage;
		}

		protected override bool IsConnected
		{
			get { return true; }
		}
	}
}
