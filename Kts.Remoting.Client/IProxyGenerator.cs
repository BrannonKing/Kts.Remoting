namespace Kts.Remoting.Client
{
	public interface IProxyGenerator
	{
		T Create<T>();
	}
}