using System;
using System.Threading;
using System.Threading.Tasks;

namespace SignalStreaming
{
    public interface ISignalTransport : IDisposable
    {
        event Action OnConnected;
        event Action OnDisconnected;
        event Action<ArraySegment<byte>> OnDataReceived;

        bool IsConnected { get; }

        void PollEvent();

        Task<bool> ConnectAsync<T>(T connectParameters, CancellationToken cancellationToken = default) where T : IConnectParameters;
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        void Send(ArraySegment<byte> data, SendOptions sendOptions, uint[] destinationClientIds = null);
    }
}
