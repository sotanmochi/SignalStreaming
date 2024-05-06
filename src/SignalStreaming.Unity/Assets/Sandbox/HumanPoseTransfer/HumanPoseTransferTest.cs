using System.Buffers;
using SignalStreaming.EngineBridge;
using SignalStreaming.Quantization;
using SignalStreaming.Serialization;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace SignalStreaming.Samples
{
    public sealed class HumanPoseTransferTest : MonoBehaviour
    {
        [SerializeField] Animator _srcAnimator;
        [SerializeField] Animator _dstAnimator;
        [SerializeField] Text _serializedDataSizeText;
        [SerializeField] Slider _musclePrecisionSlider;
        [SerializeField] Text _musclePrecisionText;
        [SerializeField] Text _requiredBitsText;

        QuantizedHumanPoseHandler _srcPoseHandler;
        QuantizedHumanPoseHandler _dstPoseHandler;
        HumanoidActorPose _sourceData;
        HumanoidActorPose _deserializedData;

        BoundedRange[] _worldBounds = new BoundedRange[]
        {
            new(-64f, 64f, 0.001f), // X
            new(-16f, 48f, 0.001f), // Y (Height)
            new(-64f, 64f, 0.001f), // Z
        };

        void Start()
        {
            SignalFormatterProvider.Register(new HumanoidActorPoseFormatter());

            var musclePrecision = Mathf.Pow(10, _musclePrecisionSlider.value);
            _srcPoseHandler = new(_srcAnimator, _worldBounds, musclePrecision);
            _dstPoseHandler = new(_dstAnimator, _worldBounds, musclePrecision);

            _sourceData = new()
            {
                InstanceId = 0,
                HumanPose = new QuantizedHumanPose(_srcPoseHandler.MuscleCount, _srcPoseHandler.RequiredBitsPerMuscleElement),
            };
            _deserializedData = new()
            {
                InstanceId = 0,
                HumanPose = new QuantizedHumanPose(_srcPoseHandler.MuscleCount, _srcPoseHandler.RequiredBitsPerMuscleElement),
            };
        }

        void Update()
        {
            var musclePrecision = Mathf.Pow(10, _musclePrecisionSlider.value);

            _srcPoseHandler.MusclePrecision = musclePrecision;
            _dstPoseHandler.MusclePrecision = musclePrecision;

            Profiler.BeginSample("HumanPoseTransferTest.SetText");
            _musclePrecisionText.text = $"Muscle Precision: {_srcPoseHandler.MusclePrecision}";
            _requiredBitsText.text = $"Required Bits Per Muscle: {_srcPoseHandler.RequiredBitsPerMuscleElement}";
            Profiler.EndSample();
        }

        void LateUpdate()
        {
            _sourceData.InstanceId = (uint)Random.Range(1, 1000);
            _sourceData.HumanPose = _srcPoseHandler.GetHumanPose();

            Profiler.BeginSample("HumanPoseTransferTest.Serialize");
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            SignalSerializer.Serialize(bufferWriter, _sourceData);
            var serializedDataMemory = bufferWriter.WrittenMemory;
            Profiler.EndSample();

            Profiler.BeginSample("HumanPoseTransferTest.SetText");
            _serializedDataSizeText.text = $"Serialized Data Size: {serializedDataMemory.Length} [bytes]";
            Profiler.EndSample();

            Profiler.BeginSample("HumanPoseTransferTest.Deserialize");
            // --------------------
            // GC allocation occurs
            // var deserializedData = SignalSerializer.Deserialize<HumanoidActorPose>(new ReadOnlySequence<byte>(serializedDataMemory));
            // _dstPoseHandler.SetHumanPose(deserializedData.HumanPose);
            // --------------------
            // Avoid GC allocation
            SignalSerializer.DeserializeTo<HumanoidActorPose>(_deserializedData, new ReadOnlySequence<byte>(serializedDataMemory));
            _dstPoseHandler.SetHumanPose(_deserializedData.HumanPose);
            // --------------------
            Profiler.EndSample();
        }
    }
}
