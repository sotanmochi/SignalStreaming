namespace SignalStreaming.Transports
{
    public interface IConnectParameters
    {
        string ServerAddress { get; set; }
        ushort ServerPort { get; set; }
        byte[] ConnectionRequestData { get; }
    }
}