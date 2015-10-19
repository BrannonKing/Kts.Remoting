using System;
using CommonSerializer;

namespace Kts.Remoting
{
	public interface IServiceWrapperGenerator
	{
		IDisposable Create(ICommonTransport transport, ICommonSerializer serializer, object service, string key);
	}
}