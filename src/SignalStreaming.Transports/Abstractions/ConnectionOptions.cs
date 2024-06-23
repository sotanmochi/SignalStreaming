namespace SignalStreaming.Transports
{
    public class TransportConnectionOptions
    {
        public string ServerAddress { get; set; } = "localhost";
        public ushort ServerPort { get; set; } = 54970;
        public byte[] ConnectionRequestData { get; set; } = new byte[0];

        public TransportConnectionOptions()
        {
        }

        public TransportConnectionOptions(string connectionRequestData)
        {
            ConnectionRequestData = System.Text.Encoding.UTF8.GetBytes(connectionRequestData);
        }
    }
}