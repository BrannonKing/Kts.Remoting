using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Kts.Remoting.Tests
{
	public class SerializerTests
	{
		public void Test1()
		{
			var data = new TestData
			{
				TestBool = true,
				TestByteArray = new byte[] {0x00, 0x02, 0x04, 0x05, 0x01},
				TestDouble = 7.0,
				TestByte = 0xff,
				TestDateTime = new DateTime(2089, 9, 27),
				TestInt = 7,
				TestList = new List<int> {4, 55, 4, 6, 7},
				TestLong = 777,
				TestShort = 456,
				TestString = "Hello World!",
				TestChar = 'R',
				TestDecimal = 100,
				TestsByte = 0x05,
				TestuInt = 80,
				DontGo = 42,
				Children = new List<SubTestData> {new SubTestData {Name = "one"}, new SubTestData {Name = "two"}}
			};
		}

		[DataContract]
		private class TestData
		{
			[DataMember(Order = 1)]
			public bool TestBool { get; set; }

			[DataMember(Order = 2)]
			public int TestInt { get; set; }

			[DataMember(Order = 3)]
			public double TestDouble { get; set; }

			[DataMember(Order = 4)]
			public long TestLong { get; set; }

			[DataMember(Order = 5)]
			public short TestShort { get; set; }

			[DataMember(Order = 6)]
			public string TestString { get; set; }

			[DataMember(Order = 7)]
			public DateTime TestDateTime { get; set; }

			[DataMember(Order = 8)]
			public byte TestByte { get; set; }

			[DataMember(Order = 9)]
			public byte[] TestByteArray { get; set; }

			[DataMember(Order = 10)]
			public List<int> TestList { get; set; }

			[DataMember(Order = 11)]
			public sbyte TestsByte { get; set; }

			[DataMember(Order = 12)]
			public uint TestuInt { get; set; }

			[DataMember(Order = 13)]
			public char TestChar { get; set; }

			[DataMember(Order = 14)]
			public decimal TestDecimal { get; set; }

			[DataMember(Order = 15)]
			public List<SubTestData> Children { get; set; }

			public int DontGo { get; set; }
		}

		[DataContract]
		private class SubTestData
		{
			[DataMember(Order = 1)]
			public string Name { get; set; }

			public override bool Equals(object obj)
			{
				return Name == ((SubTestData) obj).Name;
			}

			public override int GetHashCode()
			{
				return Name.GetHashCode();
			}
		}
	}
}