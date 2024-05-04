using System;
using System.Buffers;
using SignalStreaming.Quantization;

namespace SignalStreaming.Serialization.Formatters
{
    public sealed class QuantizedQuaternionFormatter : ISignalFormatter<QuantizedQuaternion>
    {
        public void Serialize(IBufferWriter<byte> writer, in QuantizedQuaternion value, SignalSerializerOptions options)
        {
            var bitBuffer = options.BitBuffer;
            bitBuffer.Clear();

            bitBuffer.AddUInt(value.m);
            bitBuffer.AddUInt(value.a);
            bitBuffer.AddUInt(value.b);
            bitBuffer.AddUInt(value.c);

            var span = writer.GetSpan();
            var length = bitBuffer.ToSpan(ref span);
            writer.Advance(length);
        }

        public QuantizedQuaternion Deserialize(in ReadOnlySequence<byte> byteSequence, SignalSerializerOptions options)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            var bitBuffer = options.BitBuffer;
            bitBuffer.Clear();
            bitBuffer.FromSpan(ref span, (int)byteSequence.Length);
            // TODO: Multisegments

            var m = bitBuffer.ReadUInt();
            var a = bitBuffer.ReadUInt();
            var b = bitBuffer.ReadUInt();
            var c = bitBuffer.ReadUInt();

            return new QuantizedQuaternion(m, a, b, c);
        }

        public void DeserializeTo(ref QuantizedQuaternion output, in ReadOnlySequence<byte> byteSequence, SignalSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}