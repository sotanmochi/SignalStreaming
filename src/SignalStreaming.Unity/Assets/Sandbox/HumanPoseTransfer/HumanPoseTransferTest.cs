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
        SignalSerializer _serializer = new SignalSerializer();
        QuantizedHumanPose _deserializedData;

        BoundedRange[] _worldBounds = new BoundedRange[]
        {
            new(-64f, 64f, 0.001f), // X
            new(-16f, 48f, 0.001f), // Y (Height)
            new(-64f, 64f, 0.001f), // Z
        };

        void Start()
        {
            var musclePrecision = Mathf.Pow(10, _musclePrecisionSlider.value);
            _srcPoseHandler = new(_srcAnimator, _worldBounds, musclePrecision);
            _dstPoseHandler = new(_dstAnimator, _worldBounds, musclePrecision);
            _deserializedData = new(_srcPoseHandler.MuscleCount, _srcPoseHandler.RequiredBitsPerMuscleElement);
        }

        void Update()
        {
            var musclePrecision = Mathf.Pow(10, _musclePrecisionSlider.value);

            _srcPoseHandler.MusclePrecision = musclePrecision;
            _dstPoseHandler.MusclePrecision = musclePrecision;

            _musclePrecisionText.text = $"Muscle Precision: {_srcPoseHandler.MusclePrecision}";
            _requiredBitsText.text = $"Required Bits Per Muscle: {_srcPoseHandler.RequiredBitsPerMuscleElement}";
        }

        void LateUpdate()
        {
            var sourcePose = _srcPoseHandler.GetHumanPose();

            Profiler.BeginSample("HumanPoseTransferTest.Serialize");
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            _serializer.Serialize(bufferWriter, sourcePose);
            var serializedData = bufferWriter.WrittenSpan.ToArray();
            Profiler.EndSample();

            _serializedDataSizeText.text = $"Serialized Data Size: {serializedData.Length} [bytes]";

            Profiler.BeginSample("HumanPoseTransferTest.Deserialize");
            // --------------------
            // GC allocation occurs
            // var deserializedData = _serializer.Deserialize<QuantizedHumanPose>(new ReadOnlySequence<byte>(serializedData));
            // _dstPoseHandler.SetHumanPose(deserializedData);
            // --------------------
            // Avoid GC allocation
            _serializer.DeserializeTo<QuantizedHumanPose>(_deserializedData, new ReadOnlySequence<byte>(serializedData));
            _dstPoseHandler.SetHumanPose(_deserializedData);
            // --------------------
            Profiler.EndSample();
        }
    }
}
