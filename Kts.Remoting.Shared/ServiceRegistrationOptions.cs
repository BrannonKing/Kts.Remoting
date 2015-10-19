using System;
using System.Collections.Generic;
using System.Threading;
using CommonSerializer;

namespace Kts.Remoting
{
	public class ServiceRegistrationOptions
	{
		internal Dictionary<string, object> Services = new Dictionary<string, object>();
		private CancellationToken _cancellationToken = CancellationToken.None;
		private IServiceWrapperGenerator _generator = new RoslynServiceWrapperGenerator();

		public void AddService<T>(T service)
		{
			AddService(service, typeof(T).Name);
		}

		public void AddService<T>(T service, string name)
		{
			if (service == null)
				throw new ArgumentNullException("service");
			if (string.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");

			Services.Add(name, service);
		}

		/// <summary>
		/// Use this to shutdown all the client connections.
		/// </summary>
		public CancellationToken CancellationToken
		{
			get { return _cancellationToken; }
			set { _cancellationToken = value; }
		}

		/// <summary>
		/// The engine used to generate the client-side proxy representing the specified interface.
		/// It defaults to the Roslyn class generator that comes with this framework.
		/// </summary>
		public IServiceWrapperGenerator Generator
		{
			get { return _generator; }
			set
			{
				if (value == null) throw new ArgumentNullException("value");
				_generator = value;
			}
		}



		/// <summary>
		/// Use the deflate algorithm when sending data to the client.
		/// </summary>
		public bool CompressSentMessages { get; set; }

		public ICommonSerializer Serializer { get; set; }

		/// <summary>
		/// By default, all exceptions are eaten. Subscribe here to see or do something with them.
		/// </summary>
		public event Action<Exception> OnError = delegate { };
		internal void FireOnError(Exception ex) { OnError.Invoke(ex); }

		/// <summary>
		/// Triggers after each client successfully conencts.
		/// </summary>
		public event Action OnConnected = delegate { };
		internal void FireOnConnected() { OnConnected.Invoke(); }

		/// <summary>
		/// Triggers after a client disconnect, be it requested or due to an exception.
		/// </summary>
		public event Action OnDisconnected = delegate { };
		internal void FireOnDisconnected() { OnDisconnected.Invoke(); }
	}
}
