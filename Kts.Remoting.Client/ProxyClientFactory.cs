using CommonSerializer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kts.Remoting.Client
{
	public class ProxyClientFactory
	{
		public ProxyClientFactory(ClientWebSocket socket, ICommonSerializer serializer, 
			int messageBufferSize = 2000000, bool compressSentPackets = true)
		{

		}

		public T GenerateProxy<T>() where T : class
		{

		}
	}
}
