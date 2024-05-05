using System.Buffers;

namespace SignalStreaming.Serialization
{
    public static class SignalSerializer
    {
        static SignalSerializerOptions signalSerializerOptions;

        public static SignalSerializerOptions SignalSerializerOptions
        {
            get => signalSerializerOptions ??= SignalSerializerOptions.Default;
            set => signalSerializerOptions = value;
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
                throw new System.ArgumentException($"Type {typeof(T)} is not supported");
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
                throw new System.ArgumentException($"Type {typeof(T)} is not supported");
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