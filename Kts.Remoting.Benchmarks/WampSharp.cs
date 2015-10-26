using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WampSharp.Binding;
using WampSharp.Vtortola;
using WampSharp.V2;
using WampSharp.V2.Client;
using WampSharp.V2.Core.Contracts;
using WampSharp.V2.Fluent;
using WampSharp.V2.Rpc;
using Xunit;
using Xunit.Abstractions;

namespace Kts.Remoting.Benchmarks
{
	public class WampSharp
	{
		private readonly ITestOutputHelper _testOutputHelper;

		public WampSharp(ITestOutputHelper testOutputHelper)
		{
			_testOutputHelper = testOutputHelper;
		}

		[Fact]
		public void SmallMessages()
		{

			var host = StartServer();
			var factory = new WampChannelFactory();

			var channel = factory.ConnectToRealm("realm1")
				.WebSocketTransport("ws://127.0.0.1:8080/")
				.JsonSerialization(new JsonSerializer { TypeNameHandling = TypeNameHandling.Auto })
				//.CraAuthentication(authenticationId: "peter", secret: "secret1")
				.Build();

			channel.RealmProxy.Monitor.ConnectionEstablished += (sender, eventArgs) => _testOutputHelper.WriteLine("Connected with ID " + eventArgs.SessionId);

			channel.Open().Wait();

			var proxy = channel.RealmProxy.Services.GetCalleeProxy<ISumService>(new CallerNameInterceptor());

			const int randCnt = 100;
			var rand = new Random(42);
			var randoms = new int[randCnt];
			for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);

			var sw = new Stopwatch();
			for (int j = 0; j < 200; j++)
			{
				sw.Start();
				var sum = proxy.Sum(randoms).Result;
				sw.Stop();
				Assert.Equal(randoms.Sum(), sum);
				for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);
			}

			_testOutputHelper.WriteLine("Completed 200 sum passes in {0}ms", sw.Elapsed.TotalMilliseconds);

			channel.Close();
			host.Dispose();
		}

		public class CallerNameInterceptor : ICalleeProxyInterceptor
		{
			public CallOptions GetCallOptions(MethodInfo method)
			{
				return new CallOptions();
			}

			public string GetProcedureUri(MethodInfo method)
			{
				return method.Name;
			}
		}

		private static IDisposable StartServer()
		{
			var host = new WampHost();
			host.RegisterTransport(new VtortolaWebSocketTransport(new IPEndPoint(IPAddress.Loopback, 8080), false),
				new JTokenJsonBinding());

			host.Open();

			var realm = host.RealmContainer.GetRealmByName("realm1");
			realm.Services.RegisterCallee(new SumService(), new CalleeNameInterceptor()).Wait(); // add services (aka, RPC endpoints) like this
			// realm.Services.RegisterPublisher // register some event triggerer here

			return host;
		}

		private class CalleeNameInterceptor : ICalleeRegistrationInterceptor
		{
			public bool IsCalleeProcedure(MethodInfo method)
			{
				return true;
			}

			public RegisterOptions GetRegisterOptions(MethodInfo method)
			{
				return new RegisterOptions();
			}

			public string GetProcedureUri(MethodInfo method)
			{
				return method.Name;
			}
		}
	}
}

