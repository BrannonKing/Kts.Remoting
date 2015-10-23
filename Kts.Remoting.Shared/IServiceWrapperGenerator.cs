using CommonSerializer;

namespace Kts.Remoting.Shared
{
	public interface IServiceWrapperGenerator
	{
		IMessageHandler Create(IMessageHandler messageHandler, ICommonSerializer serializer, object service);
	}
}