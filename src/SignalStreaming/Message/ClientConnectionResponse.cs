namespace SignalStreaming
{
    public readonly struct ClientConnectionResponse
    {
        public readonly bool RequestApproved;
        public readonly uint ClientId;
        public readonly string ConnectionId;
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
