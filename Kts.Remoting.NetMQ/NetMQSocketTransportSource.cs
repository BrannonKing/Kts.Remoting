﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Kts.Remoting.Shared;
using NetMQ;
using NetMQ.Sockets;

namespace Kts.Remoting.NetMQ
{
	public class NetMQSocketTransportSource : ITransportSource
	{
		private readonly NetMQSocket _socket;
		private readonly NetMQScheduler _scheduler;
		private readonly bool _createdScheduler;

		public NetMQSocketTransportSource(NetMQSocket socket, NetMQScheduler scheduler)
		{
			_socket = socket;
			_scheduler = scheduler;
			_socket.ReceiveReady += OnReceiveMessage;
		}

		public NetMQSocketTransportSource(NetMQContext context, NetMQSocket socket, Poller poller = null)
			: this(socket, new NetMQScheduler(context, poller))
		{
			_createdScheduler = true;
		}

		public virtual void Dispose()
		{
			_socket.ReceiveReady -= OnReceiveMessage;
			if (_createdScheduler)
				_scheduler.Dispose();
		}

		public event EventHandler<DataReceivedArgs> Received = delegate { };

		private void OnReceiveMessage(object sender, NetMQSocketEventArgs e)
		{
			var message = e.Socket.ReceiveMultipartMessage();
			var connectionId = e.Socket is RouterSocket && message.FrameCount > 1 ? message.First.ToByteArray() : null;
			var segment = new ArraySegment<byte>(message.Last.Buffer, 0, message.Last.MessageSize);
			Received.Invoke(this, new DataReceivedArgs { Data = segment, SessionID = connectionId });
		}

		public Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			var task = new Task(() =>
			{
				var msg = new NetMQMessage();
				if (_socket is RouterSocket)
				{
					msg.Append(new byte[0]);
					msg.AppendEmptyFrame();
					msg.Append(data.Count == data.Array.Length ? data.Array : data.ToArray());
				}

				if (connectionIDs.Length <= 0)
					_socket.SendMultipartMessage(msg);
				else
				{
					foreach (var connection in connectionIDs)
					{
						if (_socket is RouterSocket && connection is byte[])
						{
							msg.Pop();
							msg.Push(((byte[])connection));
						}
						_socket.SendMultipartMessage(msg);
					}
				}
			});
			task.Start(_scheduler);
			return task;
		}
	}
}