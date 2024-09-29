namespace SignalStreaming.Quantization
{
    public sealed class QuantizedHumanoidPose
    {
        public QuantizedVector3 RootBonePosition { get; set; }
        public QuantizedQuaternion RootBoneRotation { get; set; }
        public QuantizedVector Muscles { get; }

        public QuantizedHumanoidPose(byte muscleCount, byte requiredBitsPerElement)
        {
            Muscles = new QuantizedVector(muscleCount, requiredBitsPerElement);
        }
    }
}