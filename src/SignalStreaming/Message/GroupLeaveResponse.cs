namespace SignalStreaming
{
    public readonly struct GroupLeaveResponse
    {
        public readonly bool RequestApproved;
        public readonly string GroupId;
        public readonly string ConnectionId;

        public GroupLeaveResponse(bool requestApproved, string groupId, string connectionId)
        {
            RequestApproved = requestApproved;
            GroupId = groupId;
            ConnectionId = connectionId;
        }
    }
}
