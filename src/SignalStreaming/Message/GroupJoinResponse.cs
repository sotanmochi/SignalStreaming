using System;
using MessagePack;

namespace SignalStreaming
{
    [MessagePackObject]
    public readonly struct GroupJoinResponse
    {        
        [Key(0)]
        public readonly bool RequestApproved;
        [Key(1)]
        public readonly string GroupId;
        [Key(2)]
        public readonly string ConnectionId;

        public GroupJoinResponse(bool requestApproved, string groupId, string connectionId)
        {
            RequestApproved = requestApproved;
            GroupId = groupId;
            ConnectionId = connectionId;
        }
    }
}
