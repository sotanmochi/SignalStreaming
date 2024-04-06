#if ENABLE_MONO || ENABLE_IL2CPP
using UnityEngine;

namespace SignalStreaming.Quantization
{
    public sealed class QuantizedHumanPose
    {
        public static readonly int MuscleCount = HumanTrait.MuscleCount;

        public QuantizedVector3 BodyPosition { get; set; }
        public QuantizedQuaternion BodyRotation { get; set; }
        public QuantizedVector Muscles { get; set; } = new QuantizedVector(HumanTrait.MuscleCount);

        public static void Quantize(ref HumanPose humanPose,
            BoundedRange[] positionBoundedRange, BoundedRange muscleBoundedRange, QuantizedHumanPose output)
        {
            if (humanPose.muscles.Length != output.Muscles.Size)
            {
                throw new System.ArgumentException("Mismatched muscle count");
            }

            output.BodyPosition = BoundedRange.Quantize(humanPose.bodyPosition, positionBoundedRange);
            output.BodyRotation = SmallestThree.Quantize(humanPose.bodyRotation);
            muscleBoundedRange.Quantize(humanPose.muscles, output.Muscles);
        }

        public static void Dequantize(QuantizedHumanPose quantizedHumanPose,
            BoundedRange[] positionBoundedRange, BoundedRange muscleBoundedRange, ref HumanPose output)
        {
            if (quantizedHumanPose.Muscles.Size != output.muscles.Length)
            {
                throw new System.ArgumentException("Mismatched joint count");
            }

            output.bodyPosition = BoundedRange.Dequantize(quantizedHumanPose.BodyPosition, positionBoundedRange);
            output.bodyRotation = SmallestThree.Dequantize(quantizedHumanPose.BodyRotation);
            muscleBoundedRange.Dequantize(quantizedHumanPose.Muscles, output.muscles);
        }
    }
}
#endif