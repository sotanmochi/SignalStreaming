using System;
using System.Buffers;
using SignalStreaming.Quantization;

namespace SignalStreaming.Serialization.Formatters
{
    public sealed class QuantizedVector3Formatter : ISignalFormatter<QuantizedVector3>
    {
        public void Serialize(IBufferWriter<byte> writer, in QuantizedVector3 value, SignalSerializerOptions options)
        {
            var bitBuffer = options.BitBuffer;
            bitBuffer.Clear();

            bitBuffer.AddUInt(value.x);
            bitBuffer.AddUInt(value.y);
            bitBuffer.AddUInt(value.z);

            var span = writer.GetSpan();
            var length = bitBuffer.ToSpan(ref span);
            writer.Advance(length);
        }

        public QuantizedVector3 Deserialize(in ReadOnlySequence<byte> byteSequence, SignalSerializerOptions options)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            var bitBuffer = options.BitBuffer;
            bitBuffer.Clear();
            bitBuffer.FromSpan(ref span, (int)byteSequence.Length);
            // TODO: Multisegments

            var x = bitBuffer.ReadUInt();
            var y = bitBuffer.ReadUInt();
            var z = bitBuffer.ReadUInt();

            return new QuantizedVector3(x, y, z);
        }

        public void DeserializeTo(ref QuantizedVector3 output, in ReadOnlySequence<byte> byteSequence, SignalSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class QuantizedVectorFormatter : ISignalFormatter<QuantizedVector>
    {
        public void Serialize(IBufferWriter<byte> writer, in QuantizedVector value, SignalSerializerOptions options)
        {
            var bitBuffer = options.BitBuffer;
            bitBuffer.Clear();

            if (value.Size > bitBuffer.Length)
            {
                throw new ArgumentException($"QuantizedVector size {value.Size} is greater than the buffer size {bitBuffer.Length}");
            }

            var requiredBitsPerElement = value.RequiredBitsPerElement;
            bitBuffer.AddByte(value.Size); // Optimized
            bitBuffer.AddByte(requiredBitsPerElement); // Optimized
            for (var i = 0; i < value.Size; i++)
            {
                bitBuffer.Add(requiredBitsPerElement, value.Elements[i]); // Optimized
            }

            var span = writer.GetSpan();
            var length = bitBuffer.ToSpan(ref span);
            writer.Advance(length);
        }

        public QuantizedVector Deserialize(in ReadOnlySequence<byte> byteSequence, SignalSerializerOptions options)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            var bitBuffer = options.BitBuffer;
            bitBuffer.Clear();
            bitBuffer.FromSpan(ref span, (int)byteSequence.Length);
            // TODO: Multisegments

            var size = bitBuffer.ReadByte(); // Optimized
            var requiredBitsPerElement = bitBuffer.ReadByte(); // Optimized

            var quantizedVector = new QuantizedVector(size, requiredBitsPerElement); // Allocation

            for (var i = 0; i < size; i++)
            {
                quantizedVector.Elements[i] = bitBuffer.Read(requiredBitsPerElement); // Optimized
            }

            return quantizedVector;
        }

        public void DeserializeTo(ref QuantizedVector output, in ReadOnlySequence<byte> byteSequence, SignalSerializerOptions options)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            var bitBuffer = options.BitBuffer;
            bitBuffer.Clear();
            bitBuffer.FromSpan(ref span, (int)byteSequence.Length);
            // TODO: Multisegments

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