using System;
using System.Buffers;
using SignalStreaming.Quantization;

namespace SignalStreaming.Serialization.Formatters
{
    public sealed class QuantizedHumanPoseFormatter : ISignalFormatter<QuantizedHumanPose>
    {
        public void Serialize(IBufferWriter<byte> writer, in QuantizedHumanPose value, SignalSerializerOptions options)
        {
            var bitBuffer = options.BitBuffer;
            bitBuffer.Clear();

            // BodyPosition
            bitBuffer.AddUInt(value.BodyPosition.x);
            bitBuffer.AddUInt(value.BodyPosition.y);
            bitBuffer.AddUInt(value.BodyPosition.z);

            // BodyRotation
            bitBuffer.AddUInt(value.BodyRotation.m);
            bitBuffer.AddUInt(value.BodyRotation.a);
            bitBuffer.AddUInt(value.BodyRotation.b);
            bitBuffer.AddUInt(value.BodyRotation.c);

            // Muscles
            var requiredBitsPerElement = value.Muscles.RequiredBitsPerElement;
            bitBuffer.AddByte(value.Muscles.Size); // Optimized
            bitBuffer.AddByte(requiredBitsPerElement); // Optimized
            for (var i = 0; i < value.Muscles.Size; i++)
            {
                bitBuffer.Add(requiredBitsPerElement, value.Muscles.Elements[i]); // Optimized
            }

            var span = writer.GetSpan();
            var length = bitBuffer.ToSpan(ref span);
            writer.Advance(length);
        }

        public QuantizedHumanPose Deserialize(in ReadOnlySequence<byte> byteSequence, SignalSerializerOptions options)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            var bitBuffer = options.BitBuffer;
            bitBuffer.Clear();
            bitBuffer.FromSpan(ref span, (int)byteSequence.Length);
            // TODO: Multisegments

            // BodyPosition
            var x = bitBuffer.ReadUInt();
            var y = bitBuffer.ReadUInt();
            var z = bitBuffer.ReadUInt();

            // BodyRotation
            var m = bitBuffer.ReadUInt();
            var a = bitBuffer.ReadUInt();
            var b = bitBuffer.ReadUInt();
            var c = bitBuffer.ReadUInt();

            var muscleCount = bitBuffer.ReadByte(); // Optimized
            var requiredBitsPerElement = bitBuffer.ReadByte(); // Optimized

            var quantizedHumanPose = new QuantizedHumanPose(muscleCount, requiredBitsPerElement); // Allocation

            for (var i = 0; i < muscleCount; i++)
            {
                quantizedHumanPose.Muscles.Elements[i] = bitBuffer.Read(requiredBitsPerElement); // Optimized
            }
            quantizedHumanPose.BodyPosition = new QuantizedVector3(x, y, z);
            quantizedHumanPose.BodyRotation = new QuantizedQuaternion(m, a, b, c);

            return quantizedHumanPose;
        }

        public void DeserializeTo(ref QuantizedHumanPose output, in ReadOnlySequence<byte> byteSequence, SignalSerializerOptions options)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            var bitBuffer = options.BitBuffer;
            bitBuffer.Clear();
            bitBuffer.FromSpan(ref span, (int)byteSequence.Length);
            // TODO: Multisegments

            // BodyPosition
            var x = bitBuffer.ReadUInt();
            var y = bitBuffer.ReadUInt();
            var z = bitBuffer.ReadUInt();

            // BodyRotation
            var m = bitBuffer.ReadUInt();
            var a = bitBuffer.ReadUInt();
            var b = bitBuffer.ReadUInt();
            var c = bitBuffer.ReadUInt();

            var muscleCount = bitBuffer.ReadByte(); // Optimized
            if (muscleCount != output.Muscles.Size)
            {
                bitBuffer.Clear();
                throw new ArgumentException("Mismatched muscle count");
            }

            var requiredBitsPerElement = bitBuffer.ReadByte(); // Optimized
            for (var i = 0; i < muscleCount; i++)
            {
                output.Muscles.Elements[i] = bitBuffer.Read(requiredBitsPerElement); // Optimized
            }
            output.BodyPosition = new QuantizedVector3(x, y, z);
            output.BodyRotation = new QuantizedQuaternion(m, a, b, c);
        }
    }
}