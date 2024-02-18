using System;
using MessagePack;

namespace SignalStreaming
{
    [MessagePackObject]
    public readonly struct GroupJoinRequest
    {
        [Key(0)]
        public readonly string GroupId;
        [Key(1)]
        public readonly string ConnectionId;

        public GroupJoinRequest(string groupId, string connectionId)
        {
            GroupId = groupId;
            ConnectionId = connectionId;
        }
    }
}
