using System.Buffers;

namespace SignalStreaming.Serialization
{
    public interface ISignalFormatter<T>
    {
        void Serialize(BitBuffer buffer, in T value);
        T Deserialize(BitBuffer buffer);
        void DeserializeTo(ref T output, BitBuffer buffer);
    }
}