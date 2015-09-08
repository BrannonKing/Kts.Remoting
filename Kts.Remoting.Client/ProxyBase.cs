using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Kts.Remoting.Client
{
	class ProxyBase
	{
		protected ProxyBase(ClientWebSocket socket)
		{
			
		}

		protected Task Send(Message message)
		{

		}
	}
}
