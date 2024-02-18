using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace SignalStreaming
{
    public interface ISignalStreamingClient : IDisposable
    {
        /// <summary>
        /// Some message IDs are reserved by the core module of SignalStreaming (ID: 250 ~ 255).
        /// </summary>
        public delegate void OnDataReceivedEventHandler(int messageId, uint senderClientId, long originTimestamp, long transmitTimestamp, ReadOnlyMemory<byte> payload);

        /// <summary>
        /// Some message IDs are reserved by the core module of SignalStreaming (ID: 250 ~ 255).
        /// </summary>
        event OnDataReceivedEventHandler OnDataReceived;

        event Action<uint> OnConnected;
        event Action<string> OnDisconnected;
        event Action<GroupJoinResponse> OnGroupJoinResponseReceived;
        event Action<GroupLeaveResponse> OnGroupLeaveResponseReceived;

        uint ClientId { get; }
        bool IsConnecting { get; }
        bool IsConnected { get; }

        Task<bool> ConnectAsync<T>(T connectParameters, CancellationToken cancellationToken = default) where T : IConnectParameters;
        Task DisconnectAsync(CancellationToken cancellationToken = default);

        Task<bool> JoinGroupAsync(string groupId, CancellationToken cancellationToken = default);
        Task LeaveGroupAsync(string groupId, CancellationToken cancellationToken = default);

        void Send(int messageId, ReadOnlySequence<byte> rawMessageBuffer, SendOptions sendOptions, uint[] destinationClientIds = null);
        void Send<T>(int messageId, T data, SendOptions sendOptions, uint[] destinationClientIds = null);
    }
}
