#if ENET_CSHARP

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ENet;
using Event = ENet.Event;
using EventType = ENet.EventType;
using DebugLogger = SignalStreaming.DevelopmentOnlyLogger;

namespace SignalStreaming.Infrastructure.ENet
{
    /// <summary>
    /// Server implementation of ENet-CSharp
    /// </summary>
    public sealed class ENetTransportHub : ISignalTransportHub
    {
        class ENetClient
        {
            public Peer Peer;
            public ENetGroup Group;

            public void SetDefault()
            {
                Peer = default;
                Group = null;
            }
        }

        static readonly double TimestampsToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        readonly int _maxClients = 4000;
        readonly int _maxGroups = 500;
        readonly int _maxClientsPerGroup;

        readonly ConcurrentDictionary<string, ENetGroup> _activeGroups = new();
        readonly ENetGroup[] _groups;
        readonly ENetClient[] _connectedClients;

        readonly Thread _loopThread;
        readonly CancellationTokenSource _loopCts;
        readonly int _targetFrameTimeMilliseconds;

        byte[] _buffer; // TODO: Ring buffer and dequeue thread
        Host _server;
 
        public event Action<uint> OnConnected;
        public event Action<uint> OnDisconnected;
        public event Action<uint> OnTimeout;
        public event ISignalTransportHub.OnDataReceivedEventHandler OnDataReceived;

        public int ConnectionCapacity => _maxClients;
        public int ConnectionCount => _connectedClients.Length;

        public ENetTransportHub(ushort port, bool useAnotherThread, int targetFrameRate, bool isBackground)
        {
            _maxClientsPerGroup = _maxClients / _maxGroups;

            _groups = new ENetGroup[_maxGroups];
            for (var i = 0; i < _groups.Length; i++)
            {
                _groups[i] = new ENetGroup()
                {
                    Id = "",
                    Name = "InactiveGroup",
                    IsActive = false,
                    Clients = new Peer[_maxClientsPerGroup],
                };
            }

            _connectedClients = new ENetClient[_maxClients];
            for (var i = 0; i < _connectedClients.Length; i++)
            {
                _connectedClients[i] = new ENetClient()
                {
                    Peer = default,
                    Group = null,
                };
            }

            _buffer = new byte[1024 * 4];

            Library.Initialize();
            _server = new Host();
            _server.Create(new Address(){ Port = port }, _maxClients);

            if (useAnotherThread)
            {
                _targetFrameTimeMilliseconds = (int)(1000 / (double)targetFrameRate);

                _loopCts = new CancellationTokenSource();
                _loopThread = new Thread(RunLoop)
                {
                    Name = $"{nameof(ENetTransportHub)}",
                    IsBackground = isBackground,
                };

                // TODO: Ring buffer and dequeue thread
            }
        }

        public void Dispose()
        {
            Shutdown();
            DebugLogger.Log($"[{nameof(ENetTransportHub)}] Disposed.");
        }

        public void Start()
        {
            _loopThread?.Start();
        }

        public void Shutdown()
        {
            DebugLogger.Log($"[{nameof(ENetTransportHub)}] Shutdown...");

            DisconnectAll();

            _loopCts?.Cancel();
            _loopCts?.Dispose();

            _server.Flush();
            _server.Dispose();
            _server = null;

            Library.Deinitialize();
        }

        public void Disconnect(uint clientId)
        {
            var client = _connectedClients[clientId];
            {
                // Requests a disconnection from a peer,
                // but only after all queued outgoing packets are sent.
                client.Peer.DisconnectLater(0);
            }
        }

        public void DisconnectAll()
        {
            foreach (var client in _connectedClients)
            {
                // Requests a disconnection from a peer,
                // but only after all queued outgoing packets are sent.
                client.Peer.DisconnectLater(0);
            }
        }

        public bool TryGetGroupId(uint clientId, out string groupId)
        {
            var client = _connectedClients[clientId];
            {
                if (client.Group != null)
                {
                    groupId = client.Group.Id;
                    return true;
                }
            }

            groupId = "";
            return false;
        }

        public bool TryGetGroup(string groupId, out IGroup group)
        {
            if (_activeGroups.TryGetValue(groupId, out var enetGroup))
            {
                group = enetGroup;
                return true;
            }

            group = null;
            return false;
        }

        public bool TryAddGroup(string groupId, string groupName, out IGroup group)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                group = null;
                return false; // Invalid groupId
            }

            if (_activeGroups.Count >= _maxGroups)
            {
                group = null;
                return false; // Occupied
            }

            if (_activeGroups.TryGetValue(groupId, out var enetGroup))
            {
                group = enetGroup;
                return false; // Already exists
            }

            enetGroup = _groups.FirstOrDefault(x => !x.IsActive);
            if (enetGroup == null)
            {
                group = null;
                return false; // No groups available
            }

            _activeGroups[groupId] = enetGroup;
            enetGroup.IsActive = true;
            enetGroup.Id = groupId;
            enetGroup.Name = groupName;

            group = enetGroup;
            return true;
        }

        public bool TryRemoveGroup(string groupId)
        {
            if (_activeGroups.TryRemove(groupId, out var group))
            {
                for (var i = 0; i < group.Clients.Length; i++)
                {
                    group.Clients[i] = default;
                }

                group.Id = "";
                group.Name = "InactiveGroup";
                group.IsActive = false;

                return true;
            }

            return false;
        }

        public bool TryAddClientToGroup(uint clientId, string groupId)
        {
            if (!_connectedClients[clientId].Peer.IsSet)
            {
                return false;
            }

            if (_activeGroups.TryGetValue(groupId, out var group))
            {
                for (var i = 0; i < group.Clients.Length; i++)
                {
                    var peer = group.Clients[i];
                    if (peer.State == PeerState.Disconnected
                    || peer.State == PeerState.Uninitialized
                    || peer.State == PeerState.Zombie)
                    {
                        group.Clients[i] = _connectedClients[clientId].Peer;
                        _connectedClients[clientId].Group = group;
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryRemoveClientFromGroup(uint clientId, string groupId)
        {
            if (_activeGroups.TryGetValue(groupId, out var group))
            {
                for (var i = 0; i < group.Clients.Length; i++)
                {
                    var peer = group.Clients[i];
                    if (peer.State == PeerState.Connected)
                    {
                        if (GetClientId(peer) == clientId)
                        {
                            _connectedClients[clientId].Group = null;
                            group.Clients[i] = default;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void Send(uint destinationClientId, ArraySegment<byte> data, bool reliable)
        {
            var client = _connectedClients[destinationClientId];
            {
                var flags = reliable
                    ? (PacketFlags.Reliable | PacketFlags.NoAllocate) // Reliable Sequenced
                    : (PacketFlags.None | PacketFlags.NoAllocate); // Unreliable Sequenced

                var packet = default(Packet);
                packet.Create(data.Array, data.Count, flags);

                client.Peer.Send(channelID: 0, ref packet);
            }
        }

        public void Broadcast(string groupId, ArraySegment<byte> data, bool reliable)
        {
            if (_activeGroups.TryGetValue(groupId, out var enetGroup))
            {
                var flags = reliable
                    ? (PacketFlags.Reliable | PacketFlags.NoAllocate) // Reliable Sequenced
                    : (PacketFlags.None | PacketFlags.NoAllocate); // Unreliable Sequenced

                var packet = default(Packet);
                packet.Create(data.Array, data.Count, flags);

                _server.Broadcast(channelID: 0, ref packet, enetGroup.Clients);
            }
        }

        public void Broadcast(IReadOnlyList<uint> destinationClientIds, ArraySegment<byte> data, bool reliable)
        {
            var flags = reliable
                ? (PacketFlags.Reliable | PacketFlags.NoAllocate) // Reliable Sequenced
                : (PacketFlags.None | PacketFlags.NoAllocate); // Unreliable Sequenced

            var packet = default(Packet);
            packet.Create(data.Array, data.Count, flags);

            var peers = _connectedClients
                .Select(client => client.Peer)
                .Where(peer => destinationClientIds.Contains(GetClientId(peer)))
                .ToArray();

            _server.Broadcast(channelID: 0, ref packet, peers);
        }

        public void Broadcast(ArraySegment<byte> data, bool reliable)
        {
            var flags = reliable
                ? (PacketFlags.Reliable | PacketFlags.NoAllocate) // Reliable Sequenced
                : (PacketFlags.None | PacketFlags.NoAllocate); // Unreliable Sequenced

            var packet = default(Packet);
            packet.Create(data.Array, data.Count, flags);

            _server.Broadcast(channelID: 0, ref packet);
        }

        // TODO: Ring buffer and dequeue thread

        public void PollEvent()
        {
            var polled = false;

            while (!polled)
            {
                if (_server.CheckEvents(out var netEvent) <= 0)
                {
                    if (_server.Service(0, out netEvent) <= 0) break;
                    polled = true;
                }

                var clientId = GetClientId(netEvent.Peer);

                switch (netEvent.Type)
                {
                    case EventType.None:
                        break;
                    case EventType.Connect:
                        _connectedClients[clientId].Peer = netEvent.Peer;
                        OnConnected?.Invoke(clientId);
                        break;
                    case EventType.Disconnect:
                        _connectedClients[clientId].SetDefault();
                        OnDisconnected?.Invoke(clientId);
                        break;
                    case EventType.Timeout:
                        _connectedClients[clientId].SetDefault();
                        // REVIEW: Timeout event handling
                        break;
                    case EventType.Receive:
                        HandleReceiveEvent(netEvent);
                        break;
                }
            }

            _server.Flush();
        }

        void HandleReceiveEvent(Event netEvent)
        {
            var clientId = GetClientId(netEvent.Peer);

            var length = netEvent.Packet.Length;
            var buffer = (length <= _buffer.Length) ? _buffer : /* Temporary buffer */ new byte[length];
            Marshal.Copy(netEvent.Packet.Data, buffer, 0, length);
            netEvent.Packet.Dispose();

            // TODO: Ring buffer and dequeue thread
            OnDataReceived?.Invoke(clientId, new ArraySegment<byte>(buffer, 0, length));
        }

        void RunLoop()
        {
            while (!_loopCts.IsCancellationRequested)
            {
                var begin = Stopwatch.GetTimestamp();

                try
                {
                    PollEvent();
                }
                catch (Exception e)
                {
                    DebugLogger.LogError(e);
                }

                var end = Stopwatch.GetTimestamp();
                var elapsedTicks = (end - begin) * TimestampsToTicks;
                var elapsedMilliseconds = (long)elapsedTicks / TimeSpan.TicksPerMillisecond;

                var waitForNextFrameMilliseconds = (int)(_targetFrameTimeMilliseconds - elapsedMilliseconds);
                if (waitForNextFrameMilliseconds > 0)
                {
                    Thread.Sleep(waitForNextFrameMilliseconds);
                }
            }
        }

        uint GetClientId(Peer peer) => peer.ID + 1;
    }
}

#endif