using System;

namespace SignalStreaming
{
    public interface ISignalStreamingClientProvider
    {
        event Action OnClientAdded;
        event Action OnAliasAdded;
        event Action OnAliasUpdated;
        ISignalStreamingClient GetOrAdd(string serverUrl);
        ISignalStreamingClient GetFirstOrDefault();
        void AddOrUpdateAlias(string alias, string serverUrl);
        bool TryGet(string serverUrl, out ISignalStreamingClient client);
        bool TryGetByAlias(string alias, out ISignalStreamingClient client);
    }
}
