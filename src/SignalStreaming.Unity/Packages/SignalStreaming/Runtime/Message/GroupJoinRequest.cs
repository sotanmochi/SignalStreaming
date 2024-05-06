namespace SignalStreaming
{
    public readonly struct GroupJoinRequest
    {
        public readonly string GroupId;
        public readonly string ConnectionId;

        public GroupJoinRequest(string groupId, string connectionId)
        {
            GroupId = groupId;
            ConnectionId = connectionId;
        }
    }
}
