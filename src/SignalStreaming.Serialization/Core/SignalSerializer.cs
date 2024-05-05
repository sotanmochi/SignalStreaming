using System.Buffers;

namespace SignalStreaming.Serialization
{
    public static class SignalSerializer
    {
        public static readonly BitBuffer DefaultBitBuffer = new(256); // BufferSize = ChunkCount * 4 [bytes]

        static BitBuffer bitBuffer;

        public static BitBuffer BitBuffer
        {
            get => bitBuffer ??= DefaultBitBuffer;
            set => bitBuffer = value;
        }

        public static void Serialize<T>(IBufferWriter<byte> writer, in T value)
        {
            var formatter = SignalFormatterProvider.GetFormatter<T>();
            if (formatter != null)
            {
                BitBuffer.Clear();

                formatter.Serialize(BitBuffer, value);

                var span = writer.GetSpan();
                var length = BitBuffer.ToSpan(ref span);
                writer.Advance(length);
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
                var reader = new SequenceReader<byte>(bytes);
                var span = reader.CurrentSpan;
                BitBuffer.FromSpan(ref span, (int)bytes.Length);
                return formatter.Deserialize(BitBuffer);
            }
            else
            {
                throw new System.ArgumentException($"Type {typeof(T)} is not supported");
            }
        }

        public static void DeserializeTo<T>(T output, in ReadOnlySequence<byte> bytes) where T : class
        {
            var formatter = SignalFormatterProvider.GetFormatter<T>();
            if (formatter != null)
            {
                var reader = new SequenceReader<byte>(bytes);
                var span = reader.CurrentSpan;
                BitBuffer.FromSpan(ref span, (int)bytes.Length);
                formatter.DeserializeTo(ref output, BitBuffer);
            }
            else
            {
                throw new System.ArgumentException($"Type {typeof(T)} is not supported");
            }
        }
    }
}