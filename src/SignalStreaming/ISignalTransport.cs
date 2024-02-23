using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace SignalStreaming
{
    public interface ISignalTransport : IDisposable
    {
        event Action OnConnected;
        event Action OnDisconnected;
        event Action<ReadOnlySequence<byte>> OnIncomingSignalDequeued;

        bool IsConnected { get; }

        void DequeueIncomingSignals();
        void PollEvent();

        Task<bool> ConnectAsync<T>(T connectParameters, CancellationToken cancellationToken = default) where T : IConnectParameters;
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        void Send(ArraySegment<byte> data, SendOptions sendOptions, uint[] destinationClientIds = null);
    }
}
