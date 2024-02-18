using System;
using System.Collections.Generic;

namespace SignalStreaming
{
    public interface ISignalStreamingHub : IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        public delegate ClientConnectionResponse ConnectionRequestHandler(uint clientId, ClientConnectionRequest connectionRequest);

        /// <summary>
        /// Some message IDs are reserved by the core module of SignalStreaming (ID: 250 ~ 255).
        /// </summary>
        public delegate void OnDataReceivedEventHandler(int messageId, uint senderClientId, long originTimestamp, SendOptions sendOptions, ReadOnlyMemory<byte> payload);

        /// <summary>
        /// 
        /// </summary>
        event ConnectionRequestHandler OnClientConnectionRequested;

        /// <summary>
        /// Some message IDs are reserved by the core module of SignalStreaming (ID: 250 ~ 255).
        /// </summary>
        event OnDataReceivedEventHandler OnDataReceived;

        event Action<uint> OnClientConnected;
        event Action<uint> OnClientDisconnected;
        event Action<uint, GroupJoinRequest> OnGroupJoinRequestReceived;
        event Action<uint, GroupLeaveRequest> OnGroupLeaveRequestReceived;

        bool TryGetGroupId(uint clientId, out string groupId);
        bool TryGetGroup(string groupId, out IGroup group);
        bool TryAddGroup(string groupId, string groupName, out IGroup group);
        bool TryRemoveGroup(string groupId);
        bool TryAddClientToGroup(uint clientId, string groupId);
        bool TryRemoveClientFromGroup(uint clientId, string groupId);

        void Send<T>(int messageId, uint senderClientId, long originTimestamp, T data, bool reliable, uint destinationClientId);
        void Send(int messageId, uint senderClientId, long originTimestamp, ReadOnlyMemory<byte> rawMessagePackBlock, bool reliable, uint destinationClientId);

        void Broadcast<T>(string groupId, int messageId, T data, bool reliable, uint senderClientId, long originTimestamp);
        void Broadcast(string groupId, int messageId, ReadOnlyMemory<byte> rawMessagePackBlock, bool reliable, uint senderClientId, long originTimestamp);

        void Broadcast<T>(int messageId, uint senderClientId, long originTimestamp, T data, bool reliable, IReadOnlyList<uint> destinationClientIds);
        void Broadcast(int messageId, uint senderClientId, long originTimestamp, ReadOnlyMemory<byte> rawMessagePackBlock, bool reliable, IReadOnlyList<uint> destinationClientIds);
    }
}
