namespace SignalStreaming.Quantization
{
    public sealed class QuantizedHumanPose
    {
        public QuantizedVector3 RootBonePosition { get; set; }
        public QuantizedQuaternion RootBoneRotation { get; set; }
        public QuantizedVector Muscles { get; }

        public QuantizedHumanPose(byte muscleCount, byte requiredBitsPerElement)
        {
            Muscles = new QuantizedVector(muscleCount, requiredBitsPerElement);
        }
    }
}