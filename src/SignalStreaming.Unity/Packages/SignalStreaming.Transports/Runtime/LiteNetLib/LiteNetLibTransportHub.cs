
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LiteNetLib;
using SignalStreaming.Collections;
using DebugLogger = SignalStreaming.Transports.LiteNetLib.DevelopmentOnlyLogger;

namespace SignalStreaming.Transports.LiteNetLib
{
    /// <summary>
    /// Server implementation using LiteNetLib
    /// </summary>
    public sealed class LiteNetLibTransportHub : ISignalTransportHub, INetEventListener
    {
        class LiteNetLibClient
        {
            public NetPeer Peer;
            public LiteNetLibGroup Group;

            public void SetDefault()
            {
                Peer = null;
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

        readonly ConcurrentDictionary<string, LiteNetLibGroup> _activeGroups = new();
        readonly LiteNetLibGroup[] _groupPool;
        readonly LiteNetLibClient[] _connectedClients;

        readonly Thread _transportThread;
        readonly CancellationTokenSource _transportThreadLoopCts;
        readonly int _targetFrameTimeMilliseconds;

        readonly ConcurrentQueue<IncomingSignalDequeueRequest> _incomingSignalDequeueRequests = new();
        readonly ConcurrentQueue<OutgoingSignalDispatchRequest> _outgoingSignalDispatchRequests = new();
        readonly ConcurrentRingBuffer<byte> _incomingSignalsBuffer;
        readonly ConcurrentRingBuffer<byte> _outgoingSignalsBuffer;
        readonly byte[] _signalDispatcherBuffer;

        string _connectionKey = "SignalStreaming";
        NetManager _server;
 
        public event Action<uint> OnConnected;
        public event Action<uint> OnDisconnected;
        public event Action<uint> OnTimeout;
        public event ISignalTransportHub.OnIncomingSignalDequeuedEventHandler OnIncomingSignalDequeued;

        public int ConnectionCapacity => _maxClients;
        public int ConnectionCount => _connectedClients.Length; // TODO: Fix
        public long BytesReceived => _server.Statistics.BytesReceived;
        public long BytesSent => _server.Statistics.BytesSent;

        public LiteNetLibTransportHub(ushort port, int targetFrameRate, int maxClients = 4000, int maxGroups = 500)
        {
            _targetFrameTimeMilliseconds = (int)(1000 / (double)targetFrameRate);

            _maxClients = maxClients;
            _maxGroups = maxGroups;
            _maxClientsPerGroup = _maxClients / _maxGroups;

            _groupPool = new LiteNetLibGroup[_maxGroups];
            for (var i = 0; i < _groupPool.Length; i++)
            {
                _groupPool[i] = new LiteNetLibGroup()
                {
                    Id = "",
                    Name = "InactiveGroup",
                    IsActive = false,
                    Clients = new NetPeer[_maxClientsPerGroup],
                };
            }

            _connectedClients = new LiteNetLibClient[_maxClients];
            for (var i = 0; i < _connectedClients.Length; i++)
            {
                _connectedClients[i] = new LiteNetLibClient()
                {
                    Peer = default,
                    Group = null,
                };
            }

            _incomingSignalsBuffer = new ConcurrentRingBuffer<byte>(1024 * 4 * 4096); // 16MB
            _outgoingSignalsBuffer = new ConcurrentRingBuffer<byte>(1024 * 4 * 4096); // 16MB
            _signalDispatcherBuffer = new byte[_maxSignalSize];

            _server = new NetManager(this)
            {
                AutoRecycle = true,
                EnableStatistics = true
            };
            _server.Start(port);
            _server.BroadcastReceiveEnabled = true;

            _transportThreadLoopCts = new CancellationTokenSource();
            _transportThread = new Thread(RunTransportThreadLoop)
            {
                Name = $"{nameof(LiteNetLibTransportHub)}",
                IsBackground = true,
            };
        }

        public void Dispose()
        {
            Shutdown();
            DebugLogger.Log($"[{nameof(LiteNetLibTransportHub)}] Disposed.");
        }

        public void Start()
        {
            _transportThread?.Start();
        }

        public void Shutdown()
        {
            DebugLogger.Log($"[{nameof(LiteNetLibTransportHub)}] Shutdown...");

            DisconnectAll();

            _transportThreadLoopCts?.Cancel();
            _transportThreadLoopCts?.Dispose();

            _server.Stop();
        }

        public void Disconnect(uint clientId)
        {
            var client = _connectedClients[clientId];
            if (client.Peer != null)
            {
                _server.DisconnectPeer(client.Peer);
            }
        }

        public void DisconnectAll()
        {
            for (var i = 0; i < _connectedClients.Length; i++)
            {
                _connectedClients[i] = new LiteNetLibClient()
                {
                    Peer = default,
                    Group = null,
                };
            }

            _server.DisconnectAll();
        }

        public bool TryGetGroupId(uint clientId, out string groupId)
        {
            var client = _connectedClients[clientId];
            if (client.Group != null)
            {
                groupId = client.Group.Id;
                return true;
            }

            groupId = "";
            return false;
        }

        public bool TryGetGroup(string groupId, out IGroup group)
        {
            if (_activeGroups.TryGetValue(groupId, out var activeGroup))
            {
                group = activeGroup;
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

            if (_activeGroups.TryGetValue(groupId, out var activeGroup))
            {
                group = activeGroup;
                return false; // Already exists
            }

            var newGroup = _groupPool.FirstOrDefault(x => !x.IsActive);
            if (newGroup == null)
            {
                group = null;
                return false; // No groups available
            }

            _activeGroups[groupId] = newGroup;
            newGroup.IsActive = true;
            newGroup.Id = groupId;
            newGroup.Name = groupName;

            group = newGroup;
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
            if (_connectedClients[clientId].Peer == null)
            {
                return false;
            }

            if (_activeGroups.TryGetValue(groupId, out var group))
            {
                for (var i = 0; i < group.Clients.Length; i++)
                {
                    var peer = group.Clients[i];
                    if (peer == default)
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
                    if (peer != null)
                    {
                        if (GetClientId(peer) == clientId)
                        {
                            DebugLogger.Log($"[{nameof(LiteNetLibTransportHub)}] Client[{clientId}] removed from group {groupId}");
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

            var deliveryMethod = reliable
                ? DeliveryMethod.ReliableOrdered // Reliable Sequenced
                : DeliveryMethod.Sequenced; // Unreliable Sequenced

            client.Peer.Send(data.Array, data.Offset, data.Count, deliveryMethod);
        }

        // TODO
        // public void Send(uint destinationClientId, ReadOnlySpan<byte> data, bool reliable)
        // {
        //     var client = _connectedClients[destinationClientId];

        //     var deliveryMethod = reliable
        //         ? DeliveryMethod.ReliableOrdered // Reliable Sequenced
        //         : DeliveryMethod.Sequenced; // Unreliable Sequenced

        //     client.Peer.Send(data, deliveryMethod);
        // }

        // TODO
        // public void Broadcast(string groupId, ReadOnlySpan<byte> data, bool reliable)
        // {
        //     if (_activeGroups.TryGetValue(groupId, out var group))
        //     {
        //         var deliveryMethod = reliable
        //             ? DeliveryMethod.ReliableOrdered // Reliable Sequenced
        //             : DeliveryMethod.Sequenced; // Unreliable Sequenced

        //         foreach (var peer in group.Clients)
        //         {
        //             peer.Send(data, deliveryMethod);
        //         }
        //     }
        // }

        public void Broadcast(string groupId, ArraySegment<byte> data, bool reliable)
        {
            throw new NotImplementedException();
        }

        public void Broadcast(IReadOnlyList<uint> destinationClientIds, ArraySegment<byte> data, bool reliable)
        {
            throw new NotImplementedException();
        }

        public void Broadcast(ArraySegment<byte> data, bool reliable)
        {
            throw new NotImplementedException();
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

        void RunTransportThreadLoop()
        {
            DebugLogger.Log($"[{nameof(LiteNetLibTransportHub)}] Transport thread loop started. @Thread[{Thread.CurrentThread.ManagedThreadId}]");

            while (!_transportThreadLoopCts.IsCancellationRequested)
            {
                var begin = Stopwatch.GetTimestamp();

                try
                {
                    PollEvent(); // PollEvents();
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

            DebugLogger.Log($"[{nameof(LiteNetLibTransportHub)}] Transport thread loop exited. @Thread[{Thread.CurrentThread.ManagedThreadId}]");
        }

        void PollEvent() // PollEvents()
        {
            _server.PollEvents();
        }

        void DispatchOutgoingSignals()
        {
            var signalCount = _outgoingSignalDispatchRequests.Count;

            while (signalCount > 0)
            {
                if (_outgoingSignalDispatchRequests.TryDequeue(out var dispatchRequest))
                {
                    signalCount--;

                    var bufferLength = dispatchRequest.BufferLength;
                    var reliable = dispatchRequest.Reliable;

                    var deliveryMethod = reliable
                        ? DeliveryMethod.ReliableOrdered // Reliable Sequenced
                        : DeliveryMethod.Sequenced; // Unreliable Sequenced

                    var data = new Span<byte>(_signalDispatcherBuffer, 0, bufferLength);
                    _outgoingSignalsBuffer.TryBulkDequeue(data);

                    var destinationClientId = dispatchRequest.DestinationClientId;
                    var groupId = dispatchRequest.GroupId;

                    if (destinationClientId > 0)
                    {
                        var peer = _connectedClients[destinationClientId].Peer;
                        peer?.Send(data, deliveryMethod);
                    }
                    else if (_activeGroups.TryGetValue(groupId, out var group))
                    {
                        foreach (var peer in group.Clients)
                        {
                            peer?.Send(data, deliveryMethod);
                        }
                    }
                    else
                    {
                        _server.SendToAll(data, deliveryMethod);
                    }
                }
            }
        }

        uint GetClientId(NetPeer peer) => (uint)(peer.Id + 1);

        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
            // DebugLogger.Log($"[{nameof(LiteNetLibTransportHub)}] OnConnectionRequest");
            request.AcceptIfKey(_connectionKey);
        }

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            // DebugLogger.Log($"[{nameof(LiteNetLibTransportHub)}] OnPeerConnected");
            var clientId = GetClientId(peer);
            _connectedClients[clientId].Peer = peer;
            OnConnected?.Invoke(clientId);
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            DebugLogger.Log($"Peer disconnected {peer}, Info: {disconnectInfo.Reason}");

            var clientId = GetClientId(peer);

            var groupId = _connectedClients[clientId].Group?.Id;
            TryRemoveClientFromGroup(clientId, groupId);

            _connectedClients[clientId].SetDefault();
            OnDisconnected?.Invoke(clientId);
        }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            var length = reader.UserDataSize;
 
            var spinner = new SpinWait();
            while (!_incomingSignalsBuffer.TryBulkEnqueue(new ReadOnlySpan<byte>(reader.RawData, reader.UserDataOffset, length)))
            {
                spinner.SpinOnce();
            }

            _incomingSignalDequeueRequests.Enqueue(new IncomingSignalDequeueRequest
            {
                BufferLength = length,
                SourceClientId = GetClientId(peer),
            });
        }

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
        {
            DebugLogger.LogError($"[{nameof(LiteNetLibTransportHub)}] OnNetworkError. SocketErrorCode: {socketErrorCode}");
        }

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            DebugLogger.Log($"[{nameof(LiteNetLibTransportHub)}] OnNetworkReceiveUnconnect");
        }

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            // DebugLogger.Log($"<color=orange>{nameof(LiteNetLibTransportHub)}.OnNetworkLatencyUpdate</color>");
        }
    }
}

// #endif