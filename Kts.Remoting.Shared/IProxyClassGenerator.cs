using CommonSerializer;

namespace Kts.Remoting
{
	public interface IProxyClassGenerator
	{
		T Create<T>(ICommonTransport socket, ICommonSerializer serializer, string hubName = null) where T : class;
	}
}