using System;
using SignalStreaming.Quantization;
using UnityEngine;

namespace SignalStreaming.EngineBridge
{
    public sealed class QuantizedHumanPoseHandler
    {
        public enum MuscleType
        {
            All,
        }

        public const int AllMuscleCount = 95; // UnityEngine.HumanTrait.MuscleCount

        readonly BoundedRange[] _worldBounds;
        readonly BoundedRange _muscleBound;

        QuantizedHumanPose _quantizedHumanPose;
        HumanPose _humanPose;
        HumanPoseHandler _humanPoseHandler;

        public bool IsAvailable { get; private set; }
        public byte MuscleCount => (byte)_quantizedHumanPose.Muscles.Size;
        public byte RequiredBitsPerMuscleElement => (byte)_muscleBound.RequiredBits;
        public float MusclePrecision
        {
            get => _muscleBound.Precision;
            set
            {
                _muscleBound.Precision = value;
                _quantizedHumanPose.Muscles.RequiredBitsPerElement = (byte)_muscleBound.RequiredBits;
            }
        }

        public QuantizedHumanPoseHandler(Animator animator, BoundedRange[] worldBounds,
            float musclePrecision = 0.001f, MuscleType muscleType = MuscleType.All)
        {
            var muscleCount = muscleType switch
            {
                MuscleType.All => AllMuscleCount,
                _ => AllMuscleCount
            };

            _worldBounds = worldBounds;
            _muscleBound = new(-1f, 1f, musclePrecision);
            _quantizedHumanPose = new((byte)muscleCount, (byte)_muscleBound.RequiredBits);

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

            output.RootBonePosition = BoundedRange.Quantize(humanPose.bodyPosition, positionBoundedRange);
            output.RootBoneRotation = SmallestThree.Quantize(humanPose.bodyRotation);
            muscleBoundedRange.Quantize(humanPose.muscles, output.Muscles);
        }

        public static void Dequantize(QuantizedHumanPose quantizedHumanPose,
            BoundedRange[] positionBoundedRange, BoundedRange muscleBoundedRange, ref HumanPose output)
        {
            if (quantizedHumanPose.Muscles.Size != output.muscles.Length)
            {
                throw new System.ArgumentException("Mismatched joint count");
            }

            BoundedRange.DequantizeTo(ref output.bodyPosition, quantizedHumanPose.RootBonePosition, positionBoundedRange);
            SmallestThree.DequantizeTo(ref output.bodyRotation, quantizedHumanPose.RootBoneRotation);
            muscleBoundedRange.Dequantize(quantizedHumanPose.Muscles, output.muscles);
        }
    }
}