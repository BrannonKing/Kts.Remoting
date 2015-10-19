using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;

namespace Kts.Remoting.NetMQ
{
	public static class WebSocketExtensions
	{
		public static T RegisterInterfaceAsProxy<T>(this NetMQContext context, InterfaceRegistrationOptions options) where T : class
		{
			var mes = new ManualResetEventSlim();
			T proxy = null;
			var task = new Task(() =>
			{
				// it's tempting to let this instantiate the transport on the caller's thread
				// but all our message processing will harken back to that thread
				// which is typically the UI thread in most apps
				using (var transport = new NetMQSocketTransport(context, null, false))
				{
					proxy = options.Generator.Create<T>(transport, options.Serializer, options.ServiceName);
					mes.Set();
					transport.ProcessMessages();
				}
			}, TaskCreationOptions.LongRunning);
			task.Start();
			mes.Wait();
			mes.Dispose();
			return proxy;
		}

		public static async Task RegisterServices(this NetMQContext context, ServiceRegistrationOptions options, string nicAddress = "tcp://*:18011")
		{
			// we need router because we have to be able to send to back to specific clients
			// when the router receives a message, it needs to pull off the client ID
			// then pass the data and id to the message handler
			// and continue that task with sending a response back to the specific client
			// and we probably need to send that response on the same thread that originated the receipt

			// also, we need to subscribe for all events on the service
			// and, when a client subscribes the event locally, they need to send us a message to put it on the subscription list

			// the tricky part:
			// we need our own thread here for reading/writing messages
			// we need a separate thread pool for actually running the messages 
			// because we want them to have the right identity and not block our messages
			// and we can't block waiting to get into that threadpool

			var task = new Task(() =>
			{
				using (var transport = new NetMQSocketTransport(context, nicAddress, true))
				{
					var wrappers = new List<IDisposable>();
					foreach (var service in options.Services)
					{
						var wrapper = options.Generator.Create(transport, options.Serializer, service.Value, service.Key);
						wrappers.Add(wrapper);
					}
					// set the transport on our handler
					transport.ProcessMessages();
					wrappers.ForEach(w => w.Dispose());
				}
			}, TaskCreationOptions.LongRunning);
			task.Start();
			await task; // make sure exceptions get propagated out
		}

		private class NetMQSocketTransport : ICommonTransport
		{
			private readonly bool _appendConnection;
			private readonly NetMQScheduler _scheduler;
			private readonly Poller _poller;
			private readonly NetMQSocket _socket;

			public NetMQSocketTransport(NetMQContext context, string nicAddress, bool appendConnection)
			{
				// this class needs to not do anything with threads. Move that up the call stack.
				// it does need to make the transport, poller, and scheduler and dispose of them all in order
				// we need some method here for processing messages that blocks indefinitely
				_appendConnection = appendConnection;
				_socket = appendConnection ? (NetMQSocket)context.CreateRouterSocket() : context.CreateDealerSocket();
				_socket.ReceiveReady += OnReceiveMessage;
				if (!string.IsNullOrEmpty(nicAddress))
					_socket.Bind(nicAddress);
				_poller = new Poller(_socket);
				_scheduler = new NetMQScheduler(context, _poller);
			}

			public void Dispose()
			{
				_socket.ReceiveReady -= OnReceiveMessage;
				_scheduler.Dispose();
				_poller.Dispose();
				_socket.Dispose();
			}

			public event EventHandler<DataReceivedArgs> Received = delegate { };
			private void OnReceiveMessage(object sender, NetMQSocketEventArgs e)
			{
				var message = e.Socket.ReceiveMultipartMessage();
				var connectionId = _appendConnection ? message.First.ToByteArray() : null;
				var data = message.Last.ToByteArray();
				Received.Invoke(this, new DataReceivedArgs { Data = data, DataCount = data.Length, ConnectionID = connectionId });
			}

			public void ProcessMessages()
			{
				_poller.PollTillCancelled();
			}

			public Task Send(DataToSendArgs args)
			{
				var task = new Task(() =>
				{
					var msg = new NetMQMessage();
					foreach (var connection in args.ConnectionIDs)
					{
						msg.Clear();
						if (_appendConnection)
						{
							msg.Append(connection);
							msg.AppendEmptyFrame();
						}
						msg.Append(args.Data);
						_socket.SendMultipartMessage(msg);
					}
				});
				task.Start(_scheduler);
				return task;
			}
		}
	}
}
