namespace Sandbox.Server.SignalStreaming
{
    public sealed class SignalStreamingOptions
    {
        public ushort Port { get; set; }
        public string ConnectionKey { get; set; }

        public SignalStreamingOptions()
        {
            Port = 50030;
            ConnectionKey = "SignalStreaming";
        }
    }
}