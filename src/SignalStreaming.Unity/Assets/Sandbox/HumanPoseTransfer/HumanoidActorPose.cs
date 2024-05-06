using SignalStreaming.Quantization;

namespace SignalStreaming.Samples
{
    public sealed class HumanoidActorPose
    {
        public uint InstanceId { get; set; }
        public QuantizedHumanPose HumanPose { get; set; }
    }
}