namespace SignalStreaming
{
    public readonly struct GroupJoinResponse
    {        
        public readonly bool RequestApproved;
        public readonly string GroupId;
        public readonly string ConnectionId;

        public GroupJoinResponse(bool requestApproved, string groupId, string connectionId)
        {
            RequestApproved = requestApproved;
            GroupId = groupId;
            ConnectionId = connectionId;
        }
    }
}
