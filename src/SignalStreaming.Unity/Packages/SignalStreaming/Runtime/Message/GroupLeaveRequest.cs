using System;
using MessagePack;

namespace SignalStreaming
{
    [MessagePackObject]
    public readonly struct GroupLeaveRequest
    {
        [Key(0)]
        public readonly string GroupId;
        [Key(1)]
        public readonly string ConnectionId;

        public GroupLeaveRequest(string groupId, string connectionId)
        {
            GroupId = groupId;
            ConnectionId = connectionId;
        }
    }
}
