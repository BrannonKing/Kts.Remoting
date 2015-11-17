using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using CommonSerializer.Newtonsoft.Json;
using DDS;
using Kts.Remoting.Benchmarks;
using Kts.Remoting.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Kts.Remoting.Tests
{
	public class DdsTests
	{
		private readonly ITestOutputHelper _testOutputHelper;

		public DdsTests(ITestOutputHelper testOutputHelper)
		{
			_testOutputHelper = testOutputHelper;
		}

	
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool SetDllDirectory(string lpPathName);

		static DdsTests()
		{
			Environment.SetEnvironmentVariable("RTI_LICENSE_FILE", @"C:\Program Files\rti_connext_dds-5.2.0\rti_license.dat");
			SetDllDirectory(@"C:\Program Files\rti_connext_dds-5.2.0\lib\x64Win64VS2013\");
		}

		[Fact]
		public void BasicRoundTrip()
		{
			var serializer = new JsonCommonSerializer();

			var factory = DomainParticipantFactory.get_instance();

			var serverQos = new DomainParticipantQos();
			factory.get_default_participant_qos(serverQos);
			
			//serverQos.transport_builtin.mask = 0; // disable built-in transports
			//serverQos.property_qos.value


			serverQos.discovery.initial_peers.from_array(new[] { "1@localhost" });
			var serverParticipant = factory.create_participant(0, serverQos, null, StatusMask.STATUS_MASK_NONE);

			var serverTransport = serverParticipant.GenerateTransportSource("serverResponse", "clientRequest");
			var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
			serverRouter.AddService<EverytingToOwin.IMyService>(new EverytingToOwin.MyService());

			var clientQos = new DomainParticipantQos();
			factory.get_default_participant_qos(clientQos);
			clientQos.discovery.initial_peers.from_array(new[] { "1@localhost" });

			var clientParticipant = factory.create_participant(0, clientQos, null, StatusMask.STATUS_MASK_NONE);

			var clientTransport = clientParticipant.GenerateTransportSource("clientRequest", "serverResponse");
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<EverytingToOwin.IMyService>();

			var result = proxy.Add(3, 4).Result;
			Assert.Equal(7, result);

			clientRouter.Dispose();
			clientTransport.Dispose();
			factory.delete_participant(ref clientParticipant);
			
			serverRouter.Dispose();
			serverTransport.Dispose();
			factory.delete_participant(ref serverParticipant);
		}

		[Fact]
		public void Benchmark()
		{
			var serializer = new JsonCommonSerializer();

			var factory = DomainParticipantFactory.get_instance();

			var serverQos = new DomainParticipantQos();
			factory.get_default_participant_qos(serverQos);
			serverQos.discovery.initial_peers.from_array(new[] { "1@localhost" });

			const int maxBufferSize = 1 << 24; // 16MB
			serverQos.receiver_pool.buffer_size = 65530; // max allowed
			//serverQos.discovery_config.publication_writer_publish_mode.kind = PublishModeQosPolicyKind.ASYNCHRONOUS_PUBLISH_MODE_QOS;
			serverQos.discovery_config.publication_writer_publish_mode.flow_controller_name = FlowController.FIXED_RATE_FLOW_CONTROLLER_NAME;
			//serverQos.discovery_config.subscription_writer_publish_mode.kind = PublishModeQosPolicyKind.ASYNCHRONOUS_PUBLISH_MODE_QOS;
			serverQos.discovery_config.subscription_writer_publish_mode.flow_controller_name = FlowController.FIXED_RATE_FLOW_CONTROLLER_NAME;

			var len = serverQos.property_qos.value.length + 3;
			serverQos.property_qos.value.ensure_length(len, len);
			serverQos.property_qos.value.set_at(len - 3, new Property_t { name = "dds.transport.UDPv4.builtin.recv_socket_buffer_size", value = maxBufferSize.ToString() });
			serverQos.property_qos.value.set_at(len - 2, new Property_t { name = "dds.transport.UDPv4.builtin.parent.message_size_max", value = serverQos.receiver_pool.buffer_size.ToString() });
			serverQos.property_qos.value.set_at(len - 1, new Property_t { name = "dds.transport.UDPv4.builtin.send_socket_buffer_size", value = serverQos.receiver_pool.buffer_size.ToString() });

			//serverQos.resource_limits.type_code_max_serialized_length = maxBufferSize;
			serverQos.resource_limits.type_object_max_serialized_length = maxBufferSize;
			serverQos.resource_limits.type_object_max_deserialized_length = maxBufferSize;

			var serverParticipant = factory.create_participant(0, serverQos, null, StatusMask.STATUS_MASK_NONE);
			var controller = serverParticipant.lookup_flowcontroller(FlowController.FIXED_RATE_FLOW_CONTROLLER_NAME);
			var flowProperty = new FlowControllerProperty_t();
			controller.get_property(flowProperty);
			flowProperty.token_bucket.period = Duration_t.from_millis(50);
			controller.set_property(flowProperty);

			var serverTransport = serverParticipant.GenerateTransportSource("serverResponse", "clientRequest");
			var serverRouter = new DefaultMessageRouter(serverTransport, serializer);
			serverRouter.AddService<ISumService>(new SumService());

			var clientQos = new DomainParticipantQos();
			factory.get_default_participant_qos(clientQos);
			clientQos.discovery.initial_peers.from_array(new[] { "1@localhost" });

			clientQos.receiver_pool.buffer_size = 65530; // max allowed
			//clientQos.discovery_config.publication_writer_publish_mode.kind = PublishModeQosPolicyKind.ASYNCHRONOUS_PUBLISH_MODE_QOS;
			clientQos.discovery_config.publication_writer_publish_mode.flow_controller_name = FlowController.FIXED_RATE_FLOW_CONTROLLER_NAME;
			//clientQos.discovery_config.subscription_writer_publish_mode.kind = PublishModeQosPolicyKind.ASYNCHRONOUS_PUBLISH_MODE_QOS;
			clientQos.discovery_config.subscription_writer_publish_mode.flow_controller_name = FlowController.FIXED_RATE_FLOW_CONTROLLER_NAME;

			len = clientQos.property_qos.value.length + 3;
			clientQos.property_qos.value.ensure_length(len, len);
			clientQos.property_qos.value.set_at(len - 3, new Property_t { name = "dds.transport.UDPv4.builtin.recv_socket_buffer_size", value = maxBufferSize.ToString() });
			clientQos.property_qos.value.set_at(len - 2, new Property_t { name = "dds.transport.UDPv4.builtin.parent.message_size_max", value = clientQos.receiver_pool.buffer_size.ToString() });
			clientQos.property_qos.value.set_at(len - 1, new Property_t { name = "dds.transport.UDPv4.builtin.send_socket_buffer_size", value = clientQos.receiver_pool.buffer_size.ToString() });

			//clientQos.resource_limits.type_code_max_serialized_length = maxBufferSize;
			clientQos.resource_limits.type_object_max_serialized_length = maxBufferSize;
			clientQos.resource_limits.type_object_max_deserialized_length = maxBufferSize;

			var clientParticipant = factory.create_participant(0, clientQos, null, StatusMask.STATUS_MASK_NONE);
			controller = clientParticipant.lookup_flowcontroller(FlowController.FIXED_RATE_FLOW_CONTROLLER_NAME);
			flowProperty = new FlowControllerProperty_t();
			controller.get_property(flowProperty);
			flowProperty.token_bucket.period = Duration_t.from_millis(50);
			controller.set_property(flowProperty);


			var clientTransport = clientParticipant.GenerateTransportSource("clientRequest", "serverResponse");
			var clientRouter = new DefaultMessageRouter(clientTransport, serializer);
			var proxy = clientRouter.AddInterface<ISumService>();

			const int randCnt = 100;
			var rand = new Random(42);
			var randoms = new int[randCnt];
			for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);

			var sw = new Stopwatch();
			long timeFromClient = 0, timeToClient = 0;
			const int cnt = 1000;
			for (int j = 0; j < cnt; j++)
			{
				sw.Start();
				var sum = proxy.Sum(randoms).Result;
				sw.Stop();
				Assert.Equal(randoms.Sum(), sum);
				for (int i = 0; i < randCnt; i++) randoms[i] = rand.Next(10000000, 20000000);
				var times = proxy.TimeDiff(Stopwatch.GetTimestamp()).Result;
				timeFromClient += times.Item1;
				timeToClient += Stopwatch.GetTimestamp() - times.Item2;
			}

			_testOutputHelper.WriteLine("Completed {0} sum passes in {1}ms", cnt, sw.ElapsedMilliseconds);
			_testOutputHelper.WriteLine("Client to server latency: {0}us", timeFromClient / cnt / 10);
			_testOutputHelper.WriteLine("Server to client latency: {0}us", timeToClient / cnt / 10);

			//sw.Reset();
			//var tree = new SumServiceTree();
			//SumServiceTree.FillTree(tree, rand, 2);
			//_testOutputHelper.WriteLine("Starting large message transfer.");
			//sw.Start();
			//var result = proxy.Increment(tree).Result;
			//sw.Stop();
			//Assert.Equal(tree.Leaf + 1, result.Leaf);
			//_testOutputHelper.WriteLine("Completed large transfer in {0}ms", sw.Elapsed.TotalMilliseconds);

			clientRouter.Dispose();
			clientTransport.Dispose();
			factory.delete_participant(ref clientParticipant);

			serverRouter.Dispose();
			serverTransport.Dispose();
			factory.delete_participant(ref serverParticipant);
		}
	}
}
