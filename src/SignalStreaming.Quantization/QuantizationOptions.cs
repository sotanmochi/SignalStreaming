namespace SignalStreaming.Quantization
{
    public sealed class QuantizationOptions
    {
        public BoundedRange[] WorldBounds { get; }
        public BoundedRange MuscleBound { get; }

        public QuantizationOptions(BoundedRange[] worldBounds, BoundedRange muscleBound)
        {
            WorldBounds = worldBounds;
            MuscleBound = muscleBound;
        }
    }
}
