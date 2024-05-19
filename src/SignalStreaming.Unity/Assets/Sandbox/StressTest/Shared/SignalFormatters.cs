using System;
using SignalStreaming.Serialization;

namespace SignalStreaming.Sandbox.StressTest
{
    public sealed class ColorTypeFormatter : ISignalFormatter<ColorType>
    {
        public void Serialize(BitBuffer bitBuffer, in ColorType value)
        {
            bitBuffer.AddByte((byte)value);
        }

        public ColorType Deserialize(BitBuffer bitBuffer)
        {
            return (ColorType)bitBuffer.ReadByte();
        }

        public void DeserializeTo(ref ColorType output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class StressTestStateFormatter : ISignalFormatter<StressTestState>
    {
        public void Serialize(BitBuffer bitBuffer, in StressTestState value)
        {
            bitBuffer.AddByte((byte)value);
        }

        public StressTestState Deserialize(BitBuffer bitBuffer)
        {
            return (StressTestState)bitBuffer.ReadByte();
        }

        public void DeserializeTo(ref StressTestState output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }
}