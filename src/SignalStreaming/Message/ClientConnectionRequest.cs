namespace SignalStreaming
{
    public readonly struct ClientConnectionRequest
    {
        public readonly byte[] ConnectionKey;

        public ClientConnectionRequest(byte[] connectionKey)
        {
            ConnectionKey = connectionKey;
        }
    }
}
