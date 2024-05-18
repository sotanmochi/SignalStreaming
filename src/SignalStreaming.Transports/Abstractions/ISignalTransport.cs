using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace SignalStreaming.Transports
{
    public interface ISignalTransport : IDisposable
    {
        event Action OnConnected;
        event Action OnDisconnected;
        event Action<ReadOnlySequence<byte>> OnIncomingSignalDequeued;

        bool IsConnected { get; }
        long BytesReceived { get; }
        long BytesSent { get; }

        Task<bool> ConnectAsync<T>(T connectParameters, CancellationToken cancellationToken = default) where T : IConnectParameters;
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        void DequeueIncomingSignals();
        void EnqueueOutgoingSignal(ReadOnlySpan<byte> data, SendOptions sendOptions);

        void Send(ArraySegment<byte> data, SendOptions sendOptions, uint[] destinationClientIds = null);
    }
}
