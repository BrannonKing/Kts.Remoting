using CommonSerializer;

namespace Kts.Remoting.Shared
{
	public interface IProxyObjectGenerator
	{
		T Create<T>(IMessageHandler messageHandler, ICommonSerializer serializer, string serviceName) where T : class;
	}
}