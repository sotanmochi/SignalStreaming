using System.Buffers;

namespace SignalStreaming
{
    public interface ISignalSerializer
    {
        void Serialize<T>(IBufferWriter<byte> writer, in T value);
        T Deserialize<T>(in ReadOnlySequence<byte> bytes);
        void DeserializeTo<T>(T to, in ReadOnlySequence<byte> bytes) where T : class;
    }
}