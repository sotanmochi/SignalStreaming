#if ENET_CSHARP

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ENet;
using SignalStreaming.Collections;
using DebugLogger = SignalStreaming.Transports.ENet.DevelopmentOnlyLogger;

namespace SignalStreaming.Transports.ENet
{
    /// <summary>
    /// Server implementation of ENet-CSharp
    /// </summary>
    public sealed unsafe class ENetTransportHub : ISignalTransportHub
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

        struct IncomingSignalDequeueRequest
        {
            public int BufferLength;
            public uint SourceClientId;
        }

        struct OutgoingSignalDispatchRequest
        {
            public int BufferLength;
            public bool Reliable;
            public uint DestinationClientId;
            public string GroupId;
        }

        static readonly double TimestampsToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        readonly int _maxClients = 4000;
        readonly int _maxGroups = 500;
        readonly int _maxClientsPerGroup;
        readonly int _maxSignalSize = 1024 * 4; // 4KB

        readonly ConcurrentDictionary<string, ENetGroup> _activeGroups = new();
        readonly ENetGroup[] _groups;
        readonly ENetClient[] _connectedClients;

        readonly Thread _transportThread;
        readonly CancellationTokenSource _transportThreadLoopCts;
        readonly int _targetFrameTimeMilliseconds;

        readonly ConcurrentQueue<IncomingSignalDequeueRequest> _incomingSignalDequeueRequests = new();
        readonly ConcurrentQueue<OutgoingSignalDispatchRequest> _outgoingSignalDispatchRequests = new();
        readonly ConcurrentRingBuffer<byte> _incomingSignalsBuffer;
        readonly ConcurrentRingBuffer<byte> _outgoingSignalsBuffer;
        readonly byte[] _signalDispatcherBuffer;

        Host _server;
 
        public event Action<uint> OnConnected;
        public event Action<uint> OnDisconnected;
        public event Action<uint> OnTimeout;
        public event ISignalTransportHub.OnIncomingSignalDequeuedEventHandler OnIncomingSignalDequeued;

        public int ConnectionCapacity => _maxClients;
        public int ConnectionCount => _connectedClients.Length; // TODO: Fix
        public long BytesReceived => -1;
        public long BytesSent => -1;

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

            _incomingSignalsBuffer = new ConcurrentRingBuffer<byte>(1024 * 4 * 4096); // 16MB
            _outgoingSignalsBuffer = new ConcurrentRingBuffer<byte>(1024 * 4 * 4096); // 16MB
            _signalDispatcherBuffer = new byte[_maxSignalSize];

            Library.Initialize();
            _server = new Host();
            _server.Create(new Address(){ Port = port }, _maxClients);

            // if (useAnotherThread)
            {
                _targetFrameTimeMilliseconds = (int)(1000 / (double)targetFrameRate);

                _transportThreadLoopCts = new CancellationTokenSource();
                _transportThread = new Thread(RunTransportThreadLoop)
                {
                    Name = $"{nameof(ENetTransportHub)}",
                    IsBackground = isBackground,
                };
            }
        }

        public void Dispose()
        {
            Shutdown();
            DebugLogger.Log($"[{nameof(ENetTransportHub)}] Disposed.");
        }

        public void Start()
        {
            _transportThread?.Start();
        }

        public void Shutdown()
        {
            DebugLogger.Log($"[{nameof(ENetTransportHub)}] Shutdown...");

            DisconnectAll();

            _transportThreadLoopCts?.Cancel();
            _transportThreadLoopCts?.Dispose();

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
                if (client.Peer.IsSet)
                {
                    client.Peer.DisconnectLater(0);
                }
            }
        }

        public void DisconnectAll()
        {
            foreach (var client in _connectedClients)
            {
                // Requests a disconnection from a peer,
                // but only after all queued outgoing packets are sent.
                if (client.Peer.IsSet)
                {
                    client.Peer.DisconnectLater(0);
                }
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

        public void DequeueIncomingSignals()
        {
            var signalCount = _incomingSignalDequeueRequests.Count;

            while (signalCount > 0)
            {
                if (_incomingSignalDequeueRequests.TryDequeue(out var dequeueRequest))
                {
                    var bufferLength = dequeueRequest.BufferLength;
                    var sourceClientId = dequeueRequest.SourceClientId;

                    try
                    {
                        OnIncomingSignalDequeued?.Invoke(sourceClientId, _incomingSignalsBuffer.Slice(0, bufferLength));
                    }
                    catch (Exception e)
                    {
                        DebugLogger.LogError(e);
                    }
                    finally
                    {
                        _incomingSignalsBuffer.Clear(bufferLength);
                        signalCount--;
                    }
                }
            }
        }

        public void EnqueueOutgoingSignal(uint destinationClientId, ReadOnlySpan<byte> data, bool reliable)
        {
            var dispatchRequest = new OutgoingSignalDispatchRequest
            {
                BufferLength = data.Length,
                Reliable = reliable,
                DestinationClientId = destinationClientId,
                GroupId = "",
            };

            var spinner = new SpinWait();
            while (!_outgoingSignalsBuffer.TryBulkEnqueue(data))
            {
                spinner.SpinOnce();
            }

            _outgoingSignalDispatchRequests.Enqueue(dispatchRequest);
        }

        public void EnqueueOutgoingSignal(string groupId, ReadOnlySpan<byte> data, bool reliable)
        {
            var dispatchRequest = new OutgoingSignalDispatchRequest
            {
                BufferLength = data.Length,
                Reliable = reliable,
                DestinationClientId = 0,
                GroupId = groupId,
            };

            var spinner = new SpinWait();
            while (!_outgoingSignalsBuffer.TryBulkEnqueue(data))
            {
                spinner.SpinOnce();
            }

            _outgoingSignalDispatchRequests.Enqueue(dispatchRequest);
        }

        void PollEvent()
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

        void DispatchOutgoingSignals()
        {
            var signalCount = _outgoingSignalDispatchRequests.Count;

            while (signalCount > 0)
            {
                if (_outgoingSignalDispatchRequests.TryDequeue(out var dispatchRequest))
                {
                    var bufferLength = dispatchRequest.BufferLength;
                    var reliable = dispatchRequest.Reliable;

                    var flags = reliable
                        ? (PacketFlags.Reliable | PacketFlags.NoAllocate) // Reliable Sequenced
                        : (PacketFlags.None | PacketFlags.NoAllocate); // Unreliable Sequenced

                    var packet = default(Packet);
                    _outgoingSignalsBuffer.TryBulkDequeue(new Span<byte>(_signalDispatcherBuffer, 0, bufferLength));
                    packet.Create(_signalDispatcherBuffer, bufferLength, flags);

                    var destinationClientId = dispatchRequest.DestinationClientId;
                    var groupId = dispatchRequest.GroupId;

                    if (destinationClientId > 0)
                    {
                        var peer = _connectedClients[destinationClientId].Peer;
                        if (peer.IsSet)
                        {
                            peer.Send(channelID: 0, ref packet);
                        }
                    }
                    else if (_activeGroups.TryGetValue(groupId, out var enetGroup))
                    {
                        _server.Broadcast(channelID: 0, ref packet, enetGroup.Clients);
                    }
                    else
                    {
                        _server.Broadcast(channelID: 0, ref packet);
                    }

                    signalCount--;
                }
            }
        }

        void HandleReceiveEvent(Event netEvent)
        {
            var pointer = netEvent.Packet.Data.ToPointer();
            var length = netEvent.Packet.Length;

            var spinner = new SpinWait();
            while (!_incomingSignalsBuffer.TryBulkEnqueue(new ReadOnlySpan<byte>(pointer, length)))
            {
                spinner.SpinOnce();
            }

            _incomingSignalDequeueRequests.Enqueue(new IncomingSignalDequeueRequest
            {
                BufferLength = length,
                SourceClientId = GetClientId(netEvent.Peer),
            });

            netEvent.Packet.Dispose();
        }

        void RunTransportThreadLoop()
        {
            DebugLogger.Log($"[{nameof(ENetTransportHub)}] Transport thread loop started. @Thread[{Thread.CurrentThread.ManagedThreadId}]");

            while (!_transportThreadLoopCts.IsCancellationRequested)
            {
                var begin = Stopwatch.GetTimestamp();

                try
                {
                    PollEvent();
                    DispatchOutgoingSignals();
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

            DebugLogger.Log($"[{nameof(ENetTransportHub)}] Transport thread loop exited. @Thread[{Thread.CurrentThread.ManagedThreadId}]");
        }

        uint GetClientId(Peer peer) => peer.ID + 1;
    }
}

#endif