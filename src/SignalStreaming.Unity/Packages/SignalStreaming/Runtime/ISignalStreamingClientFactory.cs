using System;
using System.Threading;
using System.Threading.Tasks;

namespace SignalStreaming
{
    public interface ISignalStreamingClientFactory
    {
        ISignalStreamingClient Create(int transportType);
        IConnectParameters CreateConnectParameters(int transportType, string serverAddress, ushort port);
    }
}
