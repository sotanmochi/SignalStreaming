using System.Buffers;
using MessagePack;
using NetStack.Quantization;

namespace SignalStreaming.Serialization
{
    public sealed partial class SignalSerializer : ISignalSerializer
    {
        readonly MessagePackSerializerOptions _serializerOptions;

        public SignalSerializer(MessagePackSerializerOptions serializerOptions)
        {
            _serializerOptions = serializerOptions;
        }

        public void Serialize<T>(IBufferWriter<byte> writer, in T value)
        {
            if (typeof(T) == typeof(QuantizedVector3))
            {
                Serialize(writer, (QuantizedVector3)(object)value);
            }
            else if (typeof(T) == typeof(QuantizedQuaternion))
            {
                Serialize(writer, (QuantizedQuaternion)(object)value);
            }
            else
            {
                MessagePackSerializer.Serialize(writer, value, _serializerOptions);
            }
        }

        public T Deserialize<T>(in ReadOnlySequence<byte> bytes)
        {
            if (typeof(T) == typeof(QuantizedVector3))
            {
                return (T)(object)DeserializeQuantizedVector3(bytes);
            }
            else if (typeof(T) == typeof(QuantizedQuaternion))
            {
                return (T)(object)DeserializeQuantizedQuaternion(bytes);
            }
            else
            {
                return MessagePackSerializer.Deserialize<T>(bytes, _serializerOptions);
            }
        }
    }
}