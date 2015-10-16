using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommonSerializer;
using NetMQ.Sockets;
using Kts.Remoting;
using NetMQ;

namespace Kts.Remoting.NetMQ
{
	public static class WebSocketExtensions
	{

		public static T RegisterInterfaceAsProxy<T>(this DealerSocket socket, ICommonSerializer serializer) where T : class
		{
			return RegisterInterfaceAsProxy<T>(socket, serializer, new RoslynProxyObjectGenerator());
		}


		public static T RegisterInterfaceAsProxy<T>(this DealerSocket socket, ICommonSerializer serializer, IProxyObjectGenerator generator) where T : class
		{
			return generator.Create<T>(new DealerSocketTransport(socket),  serializer);
		}

		public static void RegisterService<T>(this NetMQContext context, T service, ICommonSerializer serializer, string nicAddress = "tcp://*:18011")
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

			var thread = new Thread(() =>
			{
				using (var transport = new NetMQRouterTransport(context, nicAddress))
				{
					// set the transport on our handler
					transport.ProcessMessages();
				}
			})
			{
				IsBackground = true, 
				Name = "NetMQ Listener for " + service
			};
			thread.Start();
		}

		private class NetMQRouterTransport : ICommonTransport
		{
			private readonly NetMQScheduler _scheduler;
			private readonly Poller _poller;
			private readonly RouterSocket _router;

			public NetMQRouterTransport(NetMQContext context, string nicAddress)
			{
				// this class needs to not do anything with threads. Move that up the call stack.
				// it does need to make the transport, poller, and scheduler and dispose of them all in order
				// we need some method here for processing messages that blocks indefinitely
				_router = context.CreateRouterSocket();
				_router.ReceiveReady += OnReceiveMessage;
				_router.Bind(nicAddress);
				_poller = new Poller(_router);
				_scheduler = new NetMQScheduler(context, _poller);
			}

			public void Dispose()
			{
				_router.ReceiveReady -= OnReceiveMessage;
				_scheduler.Dispose();
				_poller.Dispose();
				_router.Dispose();
			}

			public event EventHandler<DataReceivedArgs> Received = delegate { };
			private void OnReceiveMessage(object sender, NetMQSocketEventArgs e)
			{
				var message = e.Socket.ReceiveMultipartMessage();
				var connectionId = message.First.ToByteArray();
				var data = message.Last.ToByteArray();
				Received.Invoke(this, new DataReceivedArgs { Data = data, ConnectionID = connectionId });
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
						msg.Append(connection);
						msg.AppendEmptyFrame();
						msg.Append(args.Data);
						_router.SendMultipartMessage(msg);
					}
				});
				task.Start(_scheduler);
				return task;
			}
		}
	}
}
