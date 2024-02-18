namespace SignalStreaming.Infrastructure.ENet
{
    public sealed class ENetConnectParameters : IConnectParameters
    {
        public string ServerAddress { get; set; }
        public ushort ServerPort { get; set; }
        public byte[] ConnectionRequestData { get; set; } = new byte[0];
    }
}
