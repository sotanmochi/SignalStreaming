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
            var quantizedHumanPoseFormatter = SignalFormatterProvider.GetFormatter<QuantizedHumanPose>();
            if (quantizedHumanPoseFormatter == null)
            {
                throw new System.ArgumentException($"Type {typeof(QuantizedHumanPose)} is not supported");
            }

            bitBuffer.AddUInt(value.InstanceId);
            quantizedHumanPoseFormatter.Serialize(bitBuffer, value.HumanPose);
        }

        public HumanoidActorPose Deserialize(BitBuffer bitBuffer)
        {
            var quantizedHumanPoseFormatter = SignalFormatterProvider.GetFormatter<QuantizedHumanPose>();
            if (quantizedHumanPoseFormatter == null)
            {
                throw new System.ArgumentException($"ype {typeof(QuantizedHumanPose)} is not supported");
            }

            var instanceId = bitBuffer.ReadUInt();
            var quantizedHumanPose = quantizedHumanPoseFormatter.Deserialize(bitBuffer); // Allocation

            return new HumanoidActorPose
            {
                InstanceId = instanceId,
                HumanPose = quantizedHumanPose
            };
        }

        public void DeserializeTo(ref HumanoidActorPose output, BitBuffer bitBuffer)
        {
            var quantizedHumanPoseFormatter = SignalFormatterProvider.GetFormatter<QuantizedHumanPose>();
            if (quantizedHumanPoseFormatter == null)
            {
                throw new System.ArgumentException($"Type {typeof(QuantizedHumanPose)} is not supported");
            }

            var humanPoseOutput = output.HumanPose;
            output.InstanceId = bitBuffer.ReadUInt();
            quantizedHumanPoseFormatter.DeserializeTo(ref humanPoseOutput, bitBuffer);
        }
    }
}