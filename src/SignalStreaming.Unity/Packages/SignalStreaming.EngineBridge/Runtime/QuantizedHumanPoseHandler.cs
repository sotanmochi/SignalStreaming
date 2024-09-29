using System;
using SignalStreaming.Quantization;
using UnityEngine;

namespace SignalStreaming.EngineBridge
{
    public sealed class QuantizedHumanoidPoseHandler
    {
        public enum MuscleType
        {
            All,
        }

        public const int AllMuscleCount = 95; // UnityEngine.HumanTrait.MuscleCount

        readonly BoundedRange[] _worldBounds;
        readonly BoundedRange _muscleBound;

        QuantizedHumanoidPose _QuantizedHumanoidPose;
        HumanPose _humanPose;
        HumanPoseHandler _humanPoseHandler;

        public bool IsAvailable { get; private set; }
        public byte MuscleCount => (byte)_QuantizedHumanoidPose.Muscles.Size;
        public byte RequiredBitsPerMuscleElement => (byte)_muscleBound.RequiredBits;
        public float MusclePrecision
        {
            get => _muscleBound.Precision;
            set
            {
                _muscleBound.Precision = value;
                _QuantizedHumanoidPose.Muscles.RequiredBitsPerElement = (byte)_muscleBound.RequiredBits;
            }
        }

        public QuantizedHumanoidPoseHandler(Animator animator, BoundedRange[] worldBounds,
            float musclePrecision = 0.001f, MuscleType muscleType = MuscleType.All)
        {
            var muscleCount = muscleType switch
            {
                MuscleType.All => AllMuscleCount,
                _ => AllMuscleCount
            };

            _worldBounds = worldBounds;
            _muscleBound = new(-1f, 1f, musclePrecision);
            _QuantizedHumanoidPose = new((byte)muscleCount, (byte)_muscleBound.RequiredBits);

            if (animator != null)
            {
                _humanPoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                _humanPoseHandler.GetHumanPose(ref _humanPose);
                Quantize(ref _humanPose, _worldBounds, _muscleBound, _QuantizedHumanoidPose);
                IsAvailable = true;
            }
        }

        public QuantizedHumanoidPose GetHumanPose()
        {
            if (IsAvailable)
            {
                _humanPoseHandler.GetHumanPose(ref _humanPose);
                Quantize(ref _humanPose, _worldBounds, _muscleBound, _QuantizedHumanoidPose);
            }
            return _QuantizedHumanoidPose;
        }

        public void SetHumanPose(QuantizedHumanoidPose QuantizedHumanoidPose)
        {
            if (IsAvailable)
            {
                Dequantize(QuantizedHumanoidPose, _worldBounds, _muscleBound, ref _humanPose);
                _humanPoseHandler.SetHumanPose(ref _humanPose);
            }
        }

        public static void Quantize(ref HumanPose humanPose,
            BoundedRange[] positionBoundedRange, BoundedRange muscleBoundedRange, QuantizedHumanoidPose output)
        {
            if (humanPose.muscles.Length != output.Muscles.Size)
            {
                throw new System.ArgumentException("Mismatched muscle count");
            }

            output.RootBonePosition = BoundedRange.Quantize(humanPose.bodyPosition, positionBoundedRange);
            output.RootBoneRotation = SmallestThree.Quantize(humanPose.bodyRotation);
            muscleBoundedRange.Quantize(humanPose.muscles, output.Muscles);
        }

        public static void Dequantize(QuantizedHumanoidPose QuantizedHumanoidPose,
            BoundedRange[] positionBoundedRange, BoundedRange muscleBoundedRange, ref HumanPose output)
        {
            if (QuantizedHumanoidPose.Muscles.Size != output.muscles.Length)
            {
                throw new System.ArgumentException("Mismatched joint count");
            }

            BoundedRange.DequantizeTo(ref output.bodyPosition, QuantizedHumanoidPose.RootBonePosition, positionBoundedRange);
            SmallestThree.DequantizeTo(ref output.bodyRotation, QuantizedHumanoidPose.RootBoneRotation);
            muscleBoundedRange.Dequantize(QuantizedHumanoidPose.Muscles, output.muscles);
        }
    }
}