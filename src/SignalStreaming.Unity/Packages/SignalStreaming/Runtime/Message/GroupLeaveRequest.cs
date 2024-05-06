namespace SignalStreaming
{
    public readonly struct GroupLeaveRequest
    {
        public readonly string GroupId;
        public readonly string ConnectionId;

        public GroupLeaveRequest(string groupId, string connectionId)
        {
            GroupId = groupId;
            ConnectionId = connectionId;
        }
    }
}
