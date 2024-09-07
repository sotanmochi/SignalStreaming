namespace SignalStreaming
{
    public interface ISignalStreamingClientProvider
    {
        bool TryGet(string serverUrl, out ISignalStreamingClient client);
    }
}
