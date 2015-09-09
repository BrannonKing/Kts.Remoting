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

		private static readonly char[] Base62Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

		private static 

		protected static string ToBase62(long value)
		{
			var str = "";
			var len = (long)Base62Chars.Length;
			while (value != 0)
			{
				var rem = value % len;
				str += Base62Chars[(int)rem];
				value /= len;
			}
			return str;
		}

	}
}
