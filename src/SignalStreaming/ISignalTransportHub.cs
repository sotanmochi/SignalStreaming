using System;
using System.Collections.Generic;

namespace SignalStreaming
{
    public interface ISignalTransportHub : IDisposable
    {
        public delegate void OnDataReceivedEventHandler(uint senderClientId, ArraySegment<byte> data);

        event Action<uint> OnConnected;
        event Action<uint> OnDisconnected;
        event OnDataReceivedEventHandler OnDataReceived;

        void Start();
        void Shutdown();

        void PollEvent();

        void Disconnect(uint clientId);
        void DisconnectAll();

        bool TryGetGroupId(uint clientId, out string groupId);
        bool TryGetGroup(string groupId, out IGroup group);
        bool TryAddGroup(string groupId, string groupName, out IGroup group);
        bool TryRemoveGroup(string groupId);
        bool TryAddClientToGroup(uint clientId, string groupId);
        bool TryRemoveClientFromGroup(uint clientId, string groupId);

        void Send(uint destinationClientId, ArraySegment<byte> data, bool reliable);
        void Broadcast(string groupId, ArraySegment<byte> data, bool reliable);
        void Broadcast(IReadOnlyList<uint> destinationClientIds, ArraySegment<byte> data, bool reliable);
        void Broadcast(ArraySegment<byte> data, bool reliable);
    }
}
