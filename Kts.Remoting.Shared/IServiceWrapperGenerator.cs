using CommonSerializer;

namespace Kts.Remoting
{
	public interface IServiceWrapperGenerator
	{
		IMessageHandler Create(IMessageHandler messageHandler, ICommonSerializer serializer, object service);
	}
}