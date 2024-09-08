using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using SignalStreaming.Transports;

namespace SignalStreaming
{
    public interface ISignalStreamingClient : IDisposable
    {
        /// <summary>
        /// Some message IDs are reserved by the core module of SignalStreaming (ID: 250 ~ 255).
        /// </summary>
        public delegate void OnIncomingSignalDequeuedEventHandler(int signalId, ReadOnlySequence<byte> byteSequence, uint sourceClientId);

        /// <summary>
        /// Some message IDs are reserved by the core module of SignalStreaming (ID: 250 ~ 255).
        /// </summary>
        event OnIncomingSignalDequeuedEventHandler OnIncomingSignalDequeued;

        event Action<uint> OnConnected;
        event Action<string> OnDisconnected;
        event Action<GroupJoinResponse> OnGroupJoinResponseReceived;
        event Action<GroupLeaveResponse> OnGroupLeaveResponseReceived;

        uint ClientId { get; }
        bool IsConnecting { get; }
        bool IsConnected { get; }

        void Tick();

        Task<bool> ConnectAsync<T>(T options, CancellationToken cancellationToken = default) where T : TransportConnectionOptions;
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        Task<bool> JoinGroupAsync(string groupId, CancellationToken cancellationToken = default);
        Task LeaveGroupAsync(string groupId, CancellationToken cancellationToken = default);

        void Send(int messageId, ReadOnlySequence<byte> rawMessageBuffer, SendOptions sendOptions, uint[] destinationClientIds = null);
        void Send<T>(int messageId, T data, SendOptions sendOptions, uint[] destinationClientIds = null);
    }
}
