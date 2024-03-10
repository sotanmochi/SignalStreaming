using System;

namespace SignalStreaming.Samples.StressTest
{
    [Serializable]
    public class AppSettings
    {
        public bool AutoConnect { get; set; }
        public string ServerAddress { get; set; }
        public ushort Port { get; set; }
        public string ConnectionKey { get; set; }
        public string GroupId { get; set; }

        public AppSettings()
        {
            AutoConnect = false;
            ServerAddress = "localhost";
            Port = 54970;
            ConnectionKey = "SignalStreaming";
            GroupId = "01HP8DMTNKAVNQDWCBMG9NWG8S";
        }
    }
}
