using System;
using System.Buffers;
using SignalStreaming.Quantization;

namespace SignalStreaming.Serialization.Formatters
{
    public sealed class QuantizedHumanPoseFormatter : ISignalFormatter<QuantizedHumanPose>
    {
        public void Serialize(BitBuffer bitBuffer, in QuantizedHumanPose value)
        {
            // RootBonePosition
            bitBuffer.AddUInt(value.RootBonePosition.x);
            bitBuffer.AddUInt(value.RootBonePosition.y);
            bitBuffer.AddUInt(value.RootBonePosition.z);

            // RootBoneRotation
            bitBuffer.AddUInt(value.RootBoneRotation.m);
            bitBuffer.AddUInt(value.RootBoneRotation.a);
            bitBuffer.AddUInt(value.RootBoneRotation.b);
            bitBuffer.AddUInt(value.RootBoneRotation.c);

            // Muscles
            var requiredBitsPerElement = value.Muscles.RequiredBitsPerElement;
            bitBuffer.AddByte(value.Muscles.Size); // Optimized
            bitBuffer.AddByte(requiredBitsPerElement); // Optimized
            for (var i = 0; i < value.Muscles.Size; i++)
            {
                bitBuffer.Add(requiredBitsPerElement, value.Muscles.Elements[i]); // Optimized
            }
        }

        public QuantizedHumanPose Deserialize(BitBuffer bitBuffer)
        {
            // RootBonePosition
            var x = bitBuffer.ReadUInt();
            var y = bitBuffer.ReadUInt();
            var z = bitBuffer.ReadUInt();

            // RootBoneRotation
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
            quantizedHumanPose.RootBonePosition = new QuantizedVector3(x, y, z);
            quantizedHumanPose.RootBoneRotation = new QuantizedQuaternion(m, a, b, c);

            return quantizedHumanPose;
        }

        public void DeserializeTo(ref QuantizedHumanPose output, BitBuffer bitBuffer)
        {
            // RootBonePosition
            var x = bitBuffer.ReadUInt();
            var y = bitBuffer.ReadUInt();
            var z = bitBuffer.ReadUInt();

            // RootBoneRotation
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
            output.RootBonePosition = new QuantizedVector3(x, y, z);
            output.RootBoneRotation = new QuantizedQuaternion(m, a, b, c);
        }
    }
}