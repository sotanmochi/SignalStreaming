using System;
using System.Buffers;
using SignalStreaming.Quantization;

namespace SignalStreaming.Serialization.Formatters
{
    public sealed class QuantizedQuaternionFormatter : ISignalFormatter<QuantizedQuaternion>
    {
        public void Serialize(BitBuffer bitBuffer, in QuantizedQuaternion value)
        {
            bitBuffer.AddUInt(value.m);
            bitBuffer.AddUInt(value.a);
            bitBuffer.AddUInt(value.b);
            bitBuffer.AddUInt(value.c);
        }

        public QuantizedQuaternion Deserialize(BitBuffer bitBuffer)
        {
            var m = bitBuffer.ReadUInt();
            var a = bitBuffer.ReadUInt();
            var b = bitBuffer.ReadUInt();
            var c = bitBuffer.ReadUInt();
            return new QuantizedQuaternion(m, a, b, c);
        }

        public void DeserializeTo(ref QuantizedQuaternion output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }
}