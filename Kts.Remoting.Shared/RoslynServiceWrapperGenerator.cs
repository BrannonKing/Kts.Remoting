using System;
using CommonSerializer;

namespace Kts.Remoting
{
	public class RoslynServiceWrapperGenerator : IServiceWrapperGenerator
	{
			// subscribe to events on the service and forward them to the transport
			// generate a method that can handle incoming messages
		public IDisposable Create(ICommonTransport transport, ICommonSerializer serializer, object service, string serviceName = null)
		{
			throw new NotImplementedException();
		}
	}
}
