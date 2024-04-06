using System;
using SignalStreaming.Quantization;
using UnityEngine;

namespace SignalStreaming.EngineBridge
{
    public sealed class QuantizedHumanPoseHandler
    {
        public const int MuscleCount = 95; // UnityEngine.HumanTrait.MuscleCount

        readonly BoundedRange[] _worldBounds;
        readonly BoundedRange _muscleBound;

        QuantizedHumanPose _quantizedHumanPose;
        HumanPose _humanPose;
        HumanPoseHandler _humanPoseHandler;

        public bool IsAvailable { get; private set; }

        public QuantizedHumanPoseHandler(Animator animator, BoundedRange[] worldBounds, float musclePrecision = 1f / 2048)
        {
            _worldBounds = worldBounds;
            _muscleBound = new(-1f, 1f, musclePrecision);
            _quantizedHumanPose = new(MuscleCount);

            if (animator != null)
            {
                _humanPoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                _humanPoseHandler.GetHumanPose(ref _humanPose);
                Quantize(ref _humanPose, _worldBounds, _muscleBound, _quantizedHumanPose);
                IsAvailable = true;
            }
        }

        public QuantizedHumanPose GetHumanPose()
        {
            if (IsAvailable)
            {
                _humanPoseHandler.GetHumanPose(ref _humanPose);
                Quantize(ref _humanPose, _worldBounds, _muscleBound, _quantizedHumanPose);
            }
            return _quantizedHumanPose;
        }

        public void SetHumanPose(QuantizedHumanPose quantizedHumanPose)
        {
            if (IsAvailable)
            {
                Dequantize(quantizedHumanPose, _worldBounds, _muscleBound, ref _humanPose);
                _humanPoseHandler.SetHumanPose(ref _humanPose);
            }
        }

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