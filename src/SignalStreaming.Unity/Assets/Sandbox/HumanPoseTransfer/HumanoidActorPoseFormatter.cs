using System;
using System.Buffers;
using SignalStreaming.Quantization;
using SignalStreaming.Serialization;

namespace SignalStreaming.Samples
{
    public sealed class HumanoidActorPoseFormatter : ISignalFormatter<HumanoidActorPose>
    {
        public void Serialize(BitBuffer bitBuffer, in HumanoidActorPose value)
        {
            var QuantizedHumanoidPoseFormatter = SignalFormatterProvider.GetFormatter<QuantizedHumanoidPose>();
            if (QuantizedHumanoidPoseFormatter == null)
            {
                throw new System.ArgumentException($"Type {typeof(QuantizedHumanoidPose)} is not supported");
            }

            bitBuffer.AddUInt(value.InstanceId);
            QuantizedHumanoidPoseFormatter.Serialize(bitBuffer, value.HumanPose);
        }

        public HumanoidActorPose Deserialize(BitBuffer bitBuffer)
        {
            var QuantizedHumanoidPoseFormatter = SignalFormatterProvider.GetFormatter<QuantizedHumanoidPose>();
            if (QuantizedHumanoidPoseFormatter == null)
            {
                throw new System.ArgumentException($"ype {typeof(QuantizedHumanoidPose)} is not supported");
            }

            var instanceId = bitBuffer.ReadUInt();
            var QuantizedHumanoidPose = QuantizedHumanoidPoseFormatter.Deserialize(bitBuffer); // Allocation

            return new HumanoidActorPose
            {
                InstanceId = instanceId,
                HumanPose = QuantizedHumanoidPose
            };
        }

        public void DeserializeTo(ref HumanoidActorPose output, BitBuffer bitBuffer)
        {
            var QuantizedHumanoidPoseFormatter = SignalFormatterProvider.GetFormatter<QuantizedHumanoidPose>();
            if (QuantizedHumanoidPoseFormatter == null)
            {
                throw new System.ArgumentException($"Type {typeof(QuantizedHumanoidPose)} is not supported");
            }

            var humanPoseOutput = output.HumanPose;
            output.InstanceId = bitBuffer.ReadUInt();
            QuantizedHumanoidPoseFormatter.DeserializeTo(ref humanPoseOutput, bitBuffer);
        }
    }
}