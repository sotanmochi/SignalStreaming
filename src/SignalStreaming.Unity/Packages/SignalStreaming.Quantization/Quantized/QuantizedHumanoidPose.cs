namespace SignalStreaming.Quantization
{
    public sealed class QuantizedHumanPose
    {
        public QuantizedVector3 BodyPosition { get; set; }
        public QuantizedQuaternion BodyRotation { get; set; }
        public QuantizedVector Muscles { get; }

        public QuantizedHumanPose(byte muscleCount, byte requiredBitsPerElement)
        {
            Muscles = new QuantizedVector(muscleCount, requiredBitsPerElement);
        }
    }
}