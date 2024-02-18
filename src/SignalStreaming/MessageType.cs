namespace SignalStreaming
{
    /// <summary>
    /// Reserved message IDs by the core module of SignalStreaming.
    /// </summary>
    public enum MessageType : byte
    {
        Unknown = 255,
        TransportConnected = 254,
        ClientConnectionRequest = 253,
        ClientConnectionResponse = 252,
        GroupJoinRequest = 251,
        GroupJoinResponse = 250,
        GroupLeaveRequest = 249,
        GroupLeaveResponse = 248,
    }
}
