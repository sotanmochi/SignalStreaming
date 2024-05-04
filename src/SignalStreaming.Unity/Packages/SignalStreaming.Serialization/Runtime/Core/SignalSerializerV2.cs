using System.Buffers;
using MessagePack;
using SignalStreaming.Quantization;

namespace SignalStreaming.Serialization
{
    public static class SignalSerializerV2
    {
        static SignalSerializerOptions signalSerializerOptions;
        static MessagePackSerializerOptions messagePackSerializerOptions;

        public static SignalSerializerOptions SignalSerializerOptions
        {
            get => signalSerializerOptions ??= SignalSerializerOptions.Default;
            set => signalSerializerOptions = value;
        }

        public static MessagePackSerializerOptions MessagePackSerializerOptions
        {
            get => messagePackSerializerOptions;
            set => messagePackSerializerOptions = value;
        }

        public static void Serialize<T>(IBufferWriter<byte> writer, in T value)
        {
            var formatter = SignalFormatterProvider.GetFormatter<T>();
            if (formatter != null)
            {
                formatter.Serialize(writer, value, SignalSerializerOptions);
            }
            else
            {
                MessagePackSerializer.Serialize(writer, value, MessagePackSerializerOptions);
            }
        }

        public static T Deserialize<T>(in ReadOnlySequence<byte> bytes)
        {
            var formatter = SignalFormatterProvider.GetFormatter<T>();
            if (formatter != null)
            {
                return formatter.Deserialize(bytes, SignalSerializerOptions);
            }
            else
            {
                return MessagePackSerializer.Deserialize<T>(bytes, MessagePackSerializerOptions);
            }
        }

        public static void DeserializeTo<T>(T to, in ReadOnlySequence<byte> bytes) where T : class
        {
            var formatter = SignalFormatterProvider.GetFormatter<T>();
            if (formatter != null)
            {
                formatter.DeserializeTo(ref to, bytes, SignalSerializerOptions);
            }
            else
            {
                throw new System.ArgumentException($"Type {typeof(T)} is not supported");
            }
        }
    }
}