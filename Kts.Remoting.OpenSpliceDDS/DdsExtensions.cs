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

			var bdt = new ByteDataTypeSupport();
			var result = bdt.RegisterType(participant, bdt.TypeName);
			if (result != ReturnCode.Ok)
				throw new Exception("Unable to register type: " + result);

			_publisher = _participant.CreatePublisher();
			_subscriber = _participant.CreateSubscriber();

			var senderTopicQos = new TopicQos();
			participant.GetDefaultTopicQos(ref senderTopicQos);

			var receiverTopicQos = new TopicQos();
			participant.GetDefaultTopicQos(ref receiverTopicQos);

			_senderTopic = participant.CreateTopic(senderTopic, bdt.TypeName, senderTopicQos);
			_receiverTopic = participant.CreateTopic(receiverTopic, bdt.TypeName, receiverTopicQos);

			_dataWriter = (ByteDataWriter)_publisher.CreateDataWriter(_senderTopic);
			_dataToSendHandle = _dataWriter.RegisterInstance(_dataToSend);

			var dataReaderQos = new DataReaderQos();
			_subscriber.GetDefaultDataReaderQos(ref dataReaderQos);
			_dataReader = (ByteDataReader)_subscriber.CreateDataReader(_receiverTopic, dataReaderQos, this, StatusKind.Any);
		}

		private readonly ByteData _dataToSend = new ByteData();
		private InstanceHandle _dataToSendHandle;

		public Task Send(ArraySegment<byte> data, params object[] connectionIDs)
		{
			if (data.Count > _dataToSend.Bytes.Length)
				throw new ArgumentException("Expected smaller array.");

			Array.Copy(data.Array, data.Offset, _dataToSend.Bytes, 0, data.Count);
			_dataToSend.Count = data.Count;
			if (connectionIDs == null || connectionIDs.Length <= 0)
			{
				var success = _dataWriter.Put(_dataToSend, InstanceHandle.Nil);
				if (success != ReturnCode.Ok)
					throw new Exception("Not successful on write: " + success);
			}
			else
			{
				for (int i = 0; i < connectionIDs.Length; i++)
				{
					var handle = connectionIDs[i] == null ? InstanceHandle.Nil : (InstanceHandle) connectionIDs[i];
					var success = _dataWriter.Put(_dataToSend, handle);
					if (success != ReturnCode.Ok)
						throw new Exception("Not successful on write: " + success);
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
			if (success != ReturnCode.Ok)
				throw new Exception("Not successful on read: " + success);

			base.OnDataAvailable(entityInterface);

			if (data != null)
			{
				var args = new DataReceivedArgs
				{
					Data = new ArraySegment<byte>(data.Bytes, 0, data.Count),
					SessionID = info.InstanceHandle
				};
				Received.Invoke(this, args);
			}
		}

		public void Dispose()
		{
			_subscriber.DeleteDataReader(_dataReader);
			_dataWriter.UnregisterInstance(_dataToSend, _dataToSendHandle);
			_publisher.DeleteDataWriter(_dataWriter);
			_participant.DeleteTopic(_receiverTopic);
			_participant.DeleteTopic(_senderTopic);
			_participant.DeletePublisher(_publisher);
			_participant.DeleteSubscriber(_subscriber);
		}
	}

	public class ByteData
	{
		public ulong UID;
		public int Count;
		public byte[] Bytes = new byte[8192];
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

		public override string TypeName { get { return "Msgs::ByteData"; } }
		public override string[] Description { get { return new [] { "<MetaData version=\"1.0.0\"><Module name=\"Msgs\"><Struct name=\"ByteData\"><Member name=\"UID\"><ULongLong/></Member><Member name=\"Count\"><Long/></Member><Member name=\"Bytes\"><Sequence size=\"8192\"><Octet/></Sequence></Member></Struct></Module></MetaData>" }; } } // or MetaData string
		public override string KeyList { get { return "UID"; } }
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
			Write(to, offset, data.UID);
			Write(to, offset + 8, data.Count);
			for(int i = 0; i < data.Count; i++)
				Write(to, offset + 12 + i, data.Bytes[i]);
			//var objs = new object[] { data.UID, data.Count, data.Bytes };
			//Write(basePtr, to, offset, ref objs);
			//pinnedArray.Free();
			return true;
		}

		public override void CopyOut(IntPtr @from, IntPtr to)
		{
			GCHandle tmpGCHandleTo = GCHandle.FromIntPtr(to);
			object toObj = tmpGCHandleTo.Target;
			CopyOut(from, ref toObj, 0);
			tmpGCHandleTo.Target = toObj;

		}

		public override void CopyOut(IntPtr @from, ref object to, int offset)
		{
			var bd = to as ByteData;
			if (bd == null)
			{
				bd = new ByteData();
				to = bd;
			}
			
			bd.UID = ReadUInt64(@from, offset);
			bd.Count = ReadInt32(@from, offset + 8);
			for (int i = 0; i < bd.Count; i++)
				bd.Bytes[i] = ReadByte(@from, offset + 12 + i);
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
