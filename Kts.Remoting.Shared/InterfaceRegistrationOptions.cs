using System;
using System.Threading;
using CommonSerializer;

namespace Kts.Remoting
{
	public class InterfaceRegistrationOptions
	{
		public InterfaceRegistrationOptions(ICommonSerializer serializer)
		{
			Serializer = serializer; // the one required setting
		}

		private CancellationToken _cancellationToken = CancellationToken.None;
		private ICommonSerializer _serializer;
		private IProxyObjectGenerator _generator = new RoslynProxyObjectGenerator();

		/// <summary>
		/// Use this to shutdown all the connection.
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
		public IProxyObjectGenerator Generator
		{
			get { return _generator; }
			set
			{
				if (value == null) throw new ArgumentNullException("value");
				_generator = value;
			}
		}

		/// <summary>
		/// Use the deflate algorithm when sending data to the server.
		/// </summary>
		public bool CompressSentMessages { get; set; }

		/// <summary>
		/// By default the name of the interface will be used.
		/// </summary>
		public string ServiceName { get; set; }

		public ICommonSerializer Serializer
		{
			get { return _serializer; }
			set
			{
				if (value == null) throw new ArgumentNullException("value");
				_serializer = value;
			}
		}

		/// <summary>
		/// Triggers after the client successfully connects.
		/// </summary>
		public event Action OnConnected = delegate { };
		internal void FireOnConnected() { OnConnected.Invoke(); }

		/// <summary>
		/// Triggers after a client disconnect, be it requested or due to an exception.
		/// </summary>
		public event Action OnDisconnected = delegate { };
		internal void FireOnDisconnected() { OnDisconnected.Invoke(); }

		/// <summary>
		/// Triggers when a client makes a connection attempt.
		/// </summary>
		public event Action OnConnecting = delegate { };
		internal void FireOnConnecting() { OnConnecting.Invoke(); }
	}
}
