using System;
using System.Threading;
using System.Threading.Tasks;
using SignalStreaming.Infrastructure.ENet;

namespace SignalStreaming.Infrastructure
{
    public sealed class SignalStreamingClientFactory : ISignalStreamingClientFactory
    {
        public ISignalStreamingClient Create(int transportType)
        {
            var transport = new ENetTransport(useAnotherThread: true, targetFrameRate: 60, isBackground: true);
            return new SignalStreamingClient(transport);
        }

        public IConnectParameters CreateConnectParameters(int transportType, string serverAddress, ushort port)
        {
            return new ENetConnectParameters()
            {
                ServerAddress = serverAddress,
                ServerPort = port,
                // ConnectionRequestData = new byte[0],
            };
        }
    }
}
