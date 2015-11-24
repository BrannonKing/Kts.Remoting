using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DDS;
using DDS.OpenSplice;
using DDS.OpenSplice.CustomMarshalers;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public static class DdsExtensions
	{
		public static ITransportSource GenerateTransportSource(this IDomainParticipant participant, string senderTopic, string receiverTopic)
		{
			return new DomainParticipantTransportSource(participant, senderTopic, receiverTopic);
		}
	}

	public class DomainParticipantTransportSource : DataReaderListener, ITransportSource
	{
		private readonly IDomainParticipant _participant;
		private readonly ITopic _senderTopic;
		private readonly ITopic _receiverTopic;
		private readonly ByteDataWriter _dataWriter;
		private readonly ByteDataReader _dataReader;
		private readonly IPublisher _publisher;
		private readonly ISubscriber _subscriber;

		public DomainParticipantTransportSource(IDomainParticipant participant, string senderTopic, string receiverTopic)
		{
			_participant = participant;
			_publisher = _participant.CreatePublisher();
			_subscriber = _participant.CreateSubscriber();

			var senderTopicQos = new TopicQos();
			participant.GetDefaultTopicQos(ref senderTopicQos);

			var receiverTopicQos = new TopicQos();
			participant.GetDefaultTopicQos(ref receiverTopicQos);

			var bdt = new ByteDataTypeSupport();
			var result = bdt.RegisterType(participant, bdt.TypeName);

			_senderTopic = participant.CreateTopic(senderTopic, bdt.TypeName, senderTopicQos);
			_receiverTopic = participant.CreateTopic(receiverTopic, bdt.TypeName, receiverTopicQos);

			_dataWriter = (ByteDataWriter)_publisher.CreateDataWriter(_senderTopic);
			var handle = _dataWriter.RegisterInstance(_dataToSend);

			var dataReaderQos = new DataReaderQos();
			_subscriber.GetDefaultDataReaderQos(ref dataReaderQos);
			_dataReader = (ByteDataReader)_subscriber.CreateDataReader(_receiverTopic, dataReaderQos, this, StatusKind.Any);
		}

		private readonly ByteData _dataToSend = new ByteData();

		public Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			_dataToSend.Bytes = data;
			if (connectionIDs == null || connectionIDs.Length <= 0)
			{
				_dataWriter.Put(_dataToSend, InstanceHandle.Nil);
			}
			else
			{
				for (int i = 0; i < connectionIDs.Length; i++)
				{
					var handle = connectionIDs[i] == null ? InstanceHandle.Nil : (InstanceHandle) connectionIDs[i];
					_dataWriter.Put(_dataToSend, handle);
				}
			}
			return Task.FromResult(true);
		}

		public event EventHandler<DataReceivedArgs> Received = delegate { };

		public override void OnDataAvailable(IDataReader entityInterface)
		{
			var reader = (ByteDataReader)entityInterface;
			ByteData data;
			SampleInfo info;
			var success = reader.Take(out data, out info);

			base.OnDataAvailable(entityInterface);

			if (data != null)
			{
				var args = new DataReceivedArgs
				{
					Data = data.Bytes,
					SessionID = info.InstanceHandle
				};
				Received.Invoke(this, args);
			}
		}

		public void Dispose()
		{
			_subscriber.DeleteDataReader(_dataReader);
			_publisher.DeleteDataWriter(_dataWriter);
			_participant.DeleteTopic(_receiverTopic);
			_participant.DeleteTopic(_senderTopic);
			_participant.DeletePublisher(_publisher);
			_participant.DeleteSubscriber(_subscriber);
		}
	}

	public class ByteData
	{
		public ArraySegment<byte> Bytes;
	}

	public class ByteDataTypeSupportFactory : TypeSupportFactory
	{
		public override DataWriter CreateDataWriter(IntPtr gapiPtr)
		{
			return new ByteDataWriter(gapiPtr);
		}

		public override DataReader CreateDataReader(IntPtr gapiPtr)
		{
			return new ByteDataReader(gapiPtr);
		}
	}

	public class ByteDataTypeSupport : TypeSupport
	{
		public ByteDataTypeSupport() : base(typeof(ByteData), new ByteDataTypeSupportFactory())
		{
		}

		public override ReturnCode RegisterType(IDomainParticipant participant, string typeName)
		{
			return RegisterType(participant, typeName, new ByteDataMarshler());
		}

		public override string TypeName { get { return typeof(ByteData).Name; } }
		public override string[] Description { get { return new string[0]; } } // or MetaData string
		public override string KeyList { get { return "keyList"; } }
	}

	public class ByteDataMarshler : DatabaseMarshaler
	{
		public override object[] SampleReaderAlloc(int length)
		{
			return new ByteData[length];
		}

		public override bool CopyIn(IntPtr basePtr, IntPtr @from, IntPtr to)
		{
			GCHandle tmpGCHandle = GCHandle.FromIntPtr(from);
			object fromData = tmpGCHandle.Target;
			return CopyIn(basePtr, fromData, to, 0);
		}

		public override bool CopyIn(IntPtr basePtr, object @from, IntPtr to, int offset)
		{
			var data = @from as ByteData;
			if (data == null) return false;
			//var pinnedArray = GCHandle.Alloc(data.Bytes, GCHandleType.Pinned);
			//Marshal.UnsafeAddrOfPinnedArrayElement() // if offset needed
			//var pointer = pinnedArray.AddrOfPinnedObject();
			var objs = new object[] { data.Bytes };
			Write(basePtr, to, offset, ref objs);
			//pinnedArray.Free();
			return true;
		}

		public override void CopyOut(IntPtr @from, IntPtr to)
		{
			throw new NotImplementedException();
		}

		public override void CopyOut(IntPtr @from, ref object to, int offset)
		{
			throw new NotImplementedException();
		}

		public override void InitEmbeddedMarshalers(IDomainParticipant participant)
		{
			// NOP
		}
	}

	public class ByteDataReader : DataReader
	{
		public ByteDataReader(IntPtr gapiPtr) : base(gapiPtr)
		{
		}

		public ReturnCode Take(out ByteData data, out SampleInfo info)
		{
			data = new ByteData();
			object dataObjs = data;
			info = new SampleInfo();
			return FooDataReader.TakeNextSample(this, ref dataObjs, ref info);
		}
	}

	public class ByteDataWriter : DataWriter
	{
		public ByteDataWriter(IntPtr gapiPtr) : base(gapiPtr)
		{
		}

		public InstanceHandle RegisterInstance(ByteData dataToSend)
		{
			return FooDataWriter.RegisterInstance(this, dataToSend);
		}

		public ReturnCode UnregisterInstance(ByteData data, InstanceHandle handle)
		{
			return FooDataWriter.UnregisterInstance(this, data, handle);
		}

		internal ReturnCode Put(ByteData data, InstanceHandle handle)
		{
			return FooDataWriter.Write(this, data, handle);
		}
	}
}
