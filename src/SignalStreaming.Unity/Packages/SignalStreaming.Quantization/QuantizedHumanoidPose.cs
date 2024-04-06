namespace SignalStreaming.Quantization
{
    public sealed class QuantizedHumanPose
    {
        public QuantizedVector3 BodyPosition { get; set; }
        public QuantizedQuaternion BodyRotation { get; set; }
        public QuantizedVector Muscles { get; }

        public QuantizedHumanPose(int muscleCount)
        {
            Muscles = new QuantizedVector(muscleCount);
        }
    }
}