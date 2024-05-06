using System;

namespace SignalStreaming.Serialization.Formatters
{
    public sealed class ByteFormatter : ISignalFormatter<byte>
    {
        public void Serialize(BitBuffer bitBuffer, in byte value)
        {
            bitBuffer.AddByte(value);
        }

        public byte Deserialize(BitBuffer bitBuffer)
        {
            return bitBuffer.ReadByte();
        }

        public void DeserializeTo(ref byte output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class StringFormatter : ISignalFormatter<string>
    {
        public void Serialize(BitBuffer bitBuffer, in string value)
        {
            bitBuffer.AddString(value);
        }

        public string Deserialize(BitBuffer bitBuffer)
        {
            return bitBuffer.ReadString();
        }

        public void DeserializeTo(ref string output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }
}