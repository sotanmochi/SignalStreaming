using System;
using MessagePack;

namespace SignalStreaming
{
    [MessagePackObject]
    public readonly struct ClientConnectionRequest
    {
        [Key(0)]
        public readonly byte[] ConnectionKey;

        public ClientConnectionRequest(byte[] connectionKey)
        {
            ConnectionKey = connectionKey;
        }
    }
}
