using System;
using System.Threading.Tasks;
using CommonSerializer;

namespace Kts.Remoting.Shared
{
	public interface IMessageHandler
	{
		Task Handle(Message message);
	}

	public interface IMessageRouter: IMessageHandler, IDisposable
	{
		T AddInterface<T>(string nameOverride = null) where T : class;
		void AddService<T>(T service, string nameOverride = null);

		ICommonSerializer Serializer { get; }
		ITransportSource TransportSource { get; }

		IProxyObjectGenerator ProxyObjectGenerator { get; }
		IServiceWrapperGenerator ServiceWrapperGenerator { get; }


	}
}
