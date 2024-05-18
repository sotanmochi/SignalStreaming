namespace SignalStreaming.Transports.LiteNetLib
{
    public sealed class LiteNetLibConnectParameters : IConnectParameters
    {
        public string ServerAddress { get; set; }
        public ushort ServerPort { get; set; }
        public byte[] ConnectionRequestData { get; set; } = new byte[0];
    }
}
