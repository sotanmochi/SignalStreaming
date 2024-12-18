using System;

namespace SignalStreaming.Sandbox.StressTest
{
    [Serializable]
    public class AppSettings
    {
        public bool AutoConnect { get; set; }
        public bool UseCharacter { get; set; }
        public string ServerAddress { get; set; }
        public ushort Port { get; set; }
        public string ConnectionKey { get; set; }
        public string GroupId { get; set; }

        public AppSettings()
        {
            AutoConnect = false;
            UseCharacter = false;
            ServerAddress = "localhost";
            Port = 54970;
            ConnectionKey = "SignalStreaming";
            GroupId = "01HP8DMTNKAVNQDWCBMG9NWG8S";
        }
    }
}
