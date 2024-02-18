namespace SignalStreamingSamples.ConsoleAppClient
{
    public class Metrics
    {
        public uint FrameCount;
        public string Timestamp;
        public uint[] ReceivedMessageCountByClientId = new uint[SampleClient.MAX_CLIENT_COUNT];
        public float[] AveragePayloadSizeByClientId = new float[SampleClient.MAX_CLIENT_COUNT];
        public int[] MaxPayloadSizeByClientId = new int[SampleClient.MAX_CLIENT_COUNT];
        public int[] TotalPayloadSizeByClientId = new int[SampleClient.MAX_CLIENT_COUNT];
    }
}
