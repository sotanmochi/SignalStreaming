using System.Buffers;
using MessagePack;
using SignalStreaming.Quantization;

namespace SignalStreaming.Serialization
{
    public sealed partial class SignalSerializer : ISignalSerializer
    {
        readonly MessagePackSerializerOptions _serializerOptions;

        public SignalSerializer(MessagePackSerializerOptions serializerOptions = null)
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
            else if (typeof(T) == typeof(QuantizedVector))
            {
                Serialize(writer, (QuantizedVector)(object)value);
            }
            else if (typeof(T) == typeof(QuantizedHumanPose))
            {
                Serialize(writer, (QuantizedHumanPose)(object)value);
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
            else if (typeof(T) == typeof(QuantizedVector))
            {
                return (T)(object)DeserializeQuantizedVector(bytes);
            }
            else if (typeof(T) == typeof(QuantizedHumanPose))
            {
                return (T)(object)DeserializeQuantizedHumanPose(bytes);
            }
            else
            {
                return MessagePackSerializer.Deserialize<T>(bytes, _serializerOptions);
            }
        }

        public void DeserializeTo<T>(T to, in ReadOnlySequence<byte> bytes) where T : class
        {
            if (typeof(T) == typeof(QuantizedVector))
            {
                DeserializeToQuantizedVector(bytes, (QuantizedVector)(object)to);
            }
            else if (typeof(T) == typeof(QuantizedHumanPose))
            {
                DeserializeToQuantizedHumanPose(bytes, (QuantizedHumanPose)(object)to);
            }
            else
            {
                throw new System.ArgumentException($"Type {typeof(T)} is not supported");
            }
        }
    }
}