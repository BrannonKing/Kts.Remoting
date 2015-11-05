using System;
using System.Threading.Tasks;
using DDS;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public static class DdsExtensions
	{
		public static ITransportSource GenerateTransportSource(this DomainParticipant participant, string senderTopic, string receiverTopic)
		{
			return new DomainParticipantTransportSource(participant, senderTopic, receiverTopic);
		}
	}

	public class DomainParticipantTransportSource : DataReaderListener, ITransportSource
	{
		private readonly DomainParticipant _participant;
		private Topic _sender;
		private Topic _receiver;
		private DataWriter _writer;
		private DataReader _reader;

		public DomainParticipantTransportSource(DomainParticipant participant, string senderTopic, string receiverTopic)
		{
			_participant = participant;

			var senderTopicQos = new TopicQos();
			participant.get_default_topic_qos(senderTopicQos);

			var receiverTopicQos = new TopicQos();
			participant.get_default_topic_qos(receiverTopicQos);

			_sender = participant.create_topic(senderTopic, BytesTypeSupport.TYPENAME, senderTopicQos, null, StatusMask.STATUS_MASK_NONE);
			_receiver = participant.create_topic(receiverTopic, BytesTypeSupport.TYPENAME, receiverTopicQos, null, StatusMask.STATUS_MASK_NONE);

			var writerQos = new DataWriterQos();
			//writerQos.publish_mode.kind = PublishModeQosPolicyKind.ASYNCHRONOUS_PUBLISH_MODE_QOS;
			writerQos.publish_mode.flow_controller_name = FlowController.FIXED_RATE_FLOW_CONTROLLER_NAME;

			participant.get_default_datawriter_qos(writerQos);
			
			var readerQos = new DataReaderQos();
			participant.get_default_datareader_qos(readerQos);

			_writer = participant.create_datawriter(_sender, writerQos, null, StatusMask.STATUS_MASK_NONE);
			_reader = participant.create_datareader(_receiver, readerQos, this, StatusMask.STATUS_MASK_ALL);
		}

		protected override void Dispose(bool managed)
		{
			if (managed)
			{
				_participant.delete_datareader(ref _reader);
				_participant.delete_datawriter(ref _writer);
				_participant.delete_topic(ref _receiver);
				_participant.delete_topic(ref _sender);
			}
			base.Dispose(managed);
		}

		public Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			var bytes = new Bytes { value = data.Array, offset = data.Offset, length = data.Count };
			if (connectionIDs == null || connectionIDs.Length <= 0)
				_writer.write_untyped(bytes, ref InstanceHandle_t.HANDLE_NIL);
			else
			{
				for (int i = 0; i < connectionIDs.Length; i++)
				{
					var handle = connectionIDs[i] == null ? InstanceHandle_t.HANDLE_NIL : (InstanceHandle_t)connectionIDs[i];
					_writer.write_untyped(bytes, ref handle);
				}
			}
			return Task.FromResult(true);
		}

		public event EventHandler<DataReceivedArgs> Received = delegate { };

		public override void on_data_available(DataReader reader)
		{
			var bytes = new Bytes();
			var info = new SampleInfo();

			reader.take_next_sample_untyped(bytes, info);

			if (bytes.value != null)
			{
				var args = new DataReceivedArgs
				{
					Data = new ArraySegment<byte>(bytes.value, bytes.offset, bytes.length),
					SessionID = info.instance_handle
				};
				Received.Invoke(this, args);
			}
		}
	}
}
