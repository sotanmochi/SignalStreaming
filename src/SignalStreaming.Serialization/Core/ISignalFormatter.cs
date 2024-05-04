using System.Buffers;

namespace SignalStreaming.Serialization
{
    public interface ISignalFormatter<T>
    {
        void Serialize(IBufferWriter<byte> writer, in T value, SignalSerializerOptions options);
        T Deserialize(in ReadOnlySequence<byte> bytes, SignalSerializerOptions options);
        void DeserializeTo(ref T output, in ReadOnlySequence<byte> bytes, SignalSerializerOptions options);
    }
}