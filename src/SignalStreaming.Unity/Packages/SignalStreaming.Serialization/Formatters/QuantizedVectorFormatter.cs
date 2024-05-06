using System;
using System.Buffers;
using SignalStreaming.Quantization;

namespace SignalStreaming.Serialization.Formatters
{
    public sealed class QuantizedVector3Formatter : ISignalFormatter<QuantizedVector3>
    {
        public void Serialize(BitBuffer bitBuffer, in QuantizedVector3 value)
        {
            bitBuffer.AddUInt(value.x);
            bitBuffer.AddUInt(value.y);
            bitBuffer.AddUInt(value.z);
        }

        public QuantizedVector3 Deserialize(BitBuffer bitBuffer)
        {
            var x = bitBuffer.ReadUInt();
            var y = bitBuffer.ReadUInt();
            var z = bitBuffer.ReadUInt();
            return new QuantizedVector3(x, y, z);
        }

        public void DeserializeTo(ref QuantizedVector3 output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class QuantizedVectorFormatter : ISignalFormatter<QuantizedVector>
    {
        public void Serialize(BitBuffer bitBuffer, in QuantizedVector value)
        {
            var requiredBitsPerElement = value.RequiredBitsPerElement;
            bitBuffer.AddByte(value.Size); // Optimized
            bitBuffer.AddByte(requiredBitsPerElement); // Optimized
            for (var i = 0; i < value.Size; i++)
            {
                bitBuffer.Add(requiredBitsPerElement, value.Elements[i]); // Optimized
            }
        }

        public QuantizedVector Deserialize(BitBuffer bitBuffer)
        {
            var size = bitBuffer.ReadByte(); // Optimized
            var requiredBitsPerElement = bitBuffer.ReadByte(); // Optimized

            var quantizedVector = new QuantizedVector(size, requiredBitsPerElement); // Allocation

            for (var i = 0; i < size; i++)
            {
                quantizedVector.Elements[i] = bitBuffer.Read(requiredBitsPerElement); // Optimized
            }

            return quantizedVector;
        }

        public void DeserializeTo(ref QuantizedVector output, BitBuffer bitBuffer)
        {
            var size = bitBuffer.ReadByte(); // Optimized
            if (size != output.Size)
            {
                bitBuffer.Clear();
                throw new ArgumentException("Mismatched size");
            }

            var requiredBitsPerElement = bitBuffer.ReadByte(); // Optimized
            for (var i = 0; i < size; i++)
            {
                output.Elements[i] = bitBuffer.Read(requiredBitsPerElement); // Optimized
            }
        }
    }
}