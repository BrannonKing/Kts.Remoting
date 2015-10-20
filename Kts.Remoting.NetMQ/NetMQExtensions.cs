using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Monitoring;

namespace Kts.Remoting.NetMQ
{
	public static class WebSocketExtensions
	{
		public static T RegisterInterface<T>(this NetMQContext context, InterfaceRegistrationOptions options, string serverAddress = "tcp://*:18011") where T : class
		{
			var mes = new ManualResetEventSlim();
			T proxy = null;
			var task = new Task(() =>
			{
				// it's tempting to let this instantiate the transport on the caller's thread
				// but all our message processing will harken back to that thread
				// which is typically the UI thread in most apps
				using (var transport = new DealerSocketTransport(context, serverAddress, options))
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
				using (var transport = new RouterSocketTransport(context, nicAddress, options))
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

		private class DealerSocketTransport : NetMQSocketTransport
		{
			private readonly InterfaceRegistrationOptions _options;

			public DealerSocketTransport(NetMQContext context, string address, InterfaceRegistrationOptions options)
				: base(context, address, true)
			{
				_options = options;
				_monitor.Connected += OnConnected;
				_monitor.Disconnected += OnDisconnected;
				_monitor.ConnectRetried += OnConnectionRetried;
				_socket.Bind(address);
			}

			public override void Dispose()
			{
				_monitor.Connected -= OnConnected;
				_monitor.Disconnected -= OnDisconnected;
				_monitor.ConnectRetried -= OnConnectionRetried;
				base.Dispose();
			}

			private void OnConnectionRetried(object sender, NetMQMonitorIntervalEventArgs e)
			{
				_options.FireOnConnecting();
			}

			private void OnDisconnected(object sender, NetMQMonitorSocketEventArgs e)
			{
				_options.FireOnDisconnected();
			}

			private void OnConnected(object sender, NetMQMonitorSocketEventArgs e)
			{
				_options.FireOnConnected();
			}
		}

		private class RouterSocketTransport : NetMQSocketTransport
		{
			private readonly ServiceRegistrationOptions _options;

			public RouterSocketTransport(NetMQContext context, string address, ServiceRegistrationOptions options)
				:base(context, address, true)
			{
				_options = options;
				_monitor.Accepted += OnConnectionAccepted;
				_socket.Bind(address);
			}

			public override void Dispose()
			{
				_monitor.Accepted -= OnConnectionAccepted;
				base.Dispose();
			}

			private void OnConnectionAccepted(object sender, NetMQMonitorSocketEventArgs e)
			{
				_options.FireOnConnectionReceived();
			}
		}

		private abstract class NetMQSocketTransport : ICommonTransport
		{
			private readonly bool _appendConnection;
			private readonly NetMQScheduler _scheduler;
			private readonly Poller _poller;
			protected readonly NetMQSocket _socket;
			protected readonly NetMQMonitor _monitor;

			protected NetMQSocketTransport(NetMQContext context, string address, bool appendConnection)
			{
				// this class needs to not do anything with threads. Move that up the call stack.
				// it does need to make the transport, poller, and scheduler and dispose of them all in order
				// we need some method here for processing messages that blocks indefinitely
				_appendConnection = appendConnection;
				_socket = appendConnection ? (NetMQSocket)context.CreateRouterSocket() : context.CreateDealerSocket();
				_socket.ReceiveReady += OnReceiveMessage;

				_poller = new Poller(_socket);
				_scheduler = new NetMQScheduler(context, _poller);
				_monitor = new NetMQMonitor(context, _socket, address, SocketEvents.All);
				_monitor.AttachToPoller(_poller);
			}

			public virtual void Dispose()
			{
				_monitor.Dispose();
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
