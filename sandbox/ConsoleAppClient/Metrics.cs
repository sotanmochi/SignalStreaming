namespace SignalStreamingSamples.ConsoleAppClient
{
    public class Metrics
    {
        public uint FrameCount;
        public string Timestamp;
        public uint[] ReceivedMessageCountByClientId = new uint[SampleClient.MAX_CLIENT_COUNT];
        public float[] AveragePayloadSizeByClientId = new float[SampleClient.MAX_CLIENT_COUNT];
        public long[] MaxPayloadSizeByClientId = new long[SampleClient.MAX_CLIENT_COUNT];
        public long[] TotalPayloadSizeByClientId = new long[SampleClient.MAX_CLIENT_COUNT];
    }
}
