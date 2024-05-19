namespace Sandbox.StressTest.Client
{
    public class SignalStreamingOptions
    {
        public string ServerAddress { get; set; }
        public ushort ServerPort { get; set; }
        public string ConnectionKey { get; set; }
        public string GroupId { get; set; }

        public SignalStreamingOptions()
        {
            ServerAddress = "localhost";
            ServerPort = 50030;
            ConnectionKey = "";
            GroupId = "";
        }
    }
}