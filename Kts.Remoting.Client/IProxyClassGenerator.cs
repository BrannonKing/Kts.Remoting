using CommonSerializer;

namespace Kts.Remoting.Client
{
	public interface IProxyClassGenerator
	{
		T Create<T>(ICommonWebSocket socket, ICommonSerializer serializer) where T : class;
	}
}