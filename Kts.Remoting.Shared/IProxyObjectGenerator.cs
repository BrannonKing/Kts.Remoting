using CommonSerializer;

namespace Kts.Remoting
{
	public interface IProxyObjectGenerator
	{
		T Create<T>(ICommonTransport transport, ICommonSerializer serializer, string serviceName = null) where T : class;
	}
}