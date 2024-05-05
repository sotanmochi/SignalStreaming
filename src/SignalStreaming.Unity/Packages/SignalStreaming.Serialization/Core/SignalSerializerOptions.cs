namespace SignalStreaming.Serialization
{
    public class SignalSerializerOptions
    {
        public static readonly SignalSerializerOptions Default = new();

        public BitBuffer BitBuffer { get; }

        public SignalSerializerOptions(int bufferChunkCount = 256)
        {
            BitBuffer = new(bufferChunkCount); // BufferSize = ChunkCount * 4 [bytes]
        }
    }
}