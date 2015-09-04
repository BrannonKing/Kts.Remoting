using System;
using System.IO;

namespace CommonSerializer
{
	public interface ICommonSerializer
    {
        string Name { get; }
        string Description { get; }
		bool StreamsUtf8 { get; }

		ISerializedContainer GenerateContainer();
		void Serialize<T>(T t, ISerializedContainer container);

		void Serialize<T>(T t, Stream stream);
        void Serialize<T>(T t, TextWriter writer);
        string Serialize<T>(T t);

		T Deserialize<T>(ISerializedContainer container);
        T Deserialize<T>(Stream stream);
        T Deserialize<T>(TextReader reader);
        T Deserialize<T>(string str);

		object Deserialize(ISerializedContainer container, Type type);
		object Deserialize(Stream stream, Type type);
        object Deserialize(TextReader reader, Type type);
        object Deserialize(string str, Type type);

        object DeepClone(object t);
    }
}
