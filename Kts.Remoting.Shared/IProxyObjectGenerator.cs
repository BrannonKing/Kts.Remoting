using CommonSerializer;

namespace Kts.Remoting
{
	public interface IProxyObjectGenerator
	{
		T Create<T>(IMessageHandler messageHandler, ICommonSerializer serializer, string serviceName) where T : class;
	}
}