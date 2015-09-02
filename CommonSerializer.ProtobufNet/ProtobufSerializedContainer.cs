using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CommonSerializer.ProtobufNet
{
	[ProtoBuf.ProtoContract(UseProtoMembersOnly = true)]
	public class ProtobufSerializedContainer : ISerializedContainer
	{
		private MemoryStream _stream = new MemoryStream();

		public Stream Stream {  get { return _stream; } }

		[ProtoBuf.ProtoMember(1)]
		public byte[] Data
		{
			get { return _stream.ToArray(); }
			set { _stream = new MemoryStream(value, false); }
		}
	}
}
