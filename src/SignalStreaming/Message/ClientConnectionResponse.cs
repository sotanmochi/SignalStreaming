using System;
using MessagePack;

namespace SignalStreaming
{
    [MessagePackObject]
    public readonly struct ClientConnectionResponse
    {
        [Key(0)]
        public readonly bool RequestApproved;
        [Key(1)]
        public readonly uint ClientId;
        [Key(2)]
        public readonly string ConnectionId;
        [Key(3)]
        public readonly string Message;

        public ClientConnectionResponse(bool requestApproved, uint clientId, string connectionId, string message)
        {
            RequestApproved = requestApproved;
            ClientId = clientId;
            ConnectionId = connectionId;
            Message = message;
        }
    }
}
