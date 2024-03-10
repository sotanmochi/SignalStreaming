// #if LITE_NET_LIB

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using SignalStreaming.Collections;
using DebugLogger = SignalStreaming.DevelopmentOnlyLogger;

namespace SignalStreaming.Infrastructure.LiteNetLib
{
    /// <summary>
    /// Client implementation using LiteNetLib
    /// </summary>
    public sealed class LiteNetLibTransport : ISignalTransport, INetEventListener
    {
        struct IncomingSignalDequeueRequest
        {
            public int BufferLength;
            public uint SourceClientId;
        }

        struct OutgoingSignalDispatchRequest
        {
            public int BufferLength;
            public bool Reliable;
        }

        static readonly double TimestampsToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        readonly int _maxSignalSize = 1024 * 4; // 4KB

        readonly Thread _transportThread;
        readonly CancellationTokenSource _transportThreadLoopCts;
        readonly int _targetFrameTimeMilliseconds;

        readonly ConcurrentQueue<IncomingSignalDequeueRequest> _incomingSignalDequeueRequests = new();
        readonly ConcurrentQueue<OutgoingSignalDispatchRequest> _outgoingSignalDispatchRequests = new();
        readonly ConcurrentRingBuffer<byte> _incomingSignalsBuffer;
        readonly ConcurrentRingBuffer<byte> _outgoingSignalsBuffer;
        readonly byte[] _signalDispatcherBuffer;

        string _serverAddress;
        ushort _serverPort;
        string _connectionKey = "SignalStreaming";

        NetManager _client;
        NetPeer _peer;

        TaskCompletionSource<bool> _connectionTcs;
        bool _connected;
        bool _connecting;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<ReadOnlySequence<byte>> OnIncomingSignalDequeued;

        public bool IsConnected => _connected;
        public long BytesReceived => _client.Statistics.BytesReceived;
        public long BytesSent => _client.Statistics.BytesSent;

        public LiteNetLibTransport(int targetFrameRate)
        {
            _targetFrameTimeMilliseconds = (int)(1000 / (double)targetFrameRate);

            _incomingSignalsBuffer = new ConcurrentRingBuffer<byte>(1024 * 4 * 4096); // 16MB
            _outgoingSignalsBuffer = new ConcurrentRingBuffer<byte>(1024 * 4 * 4096); // 16MB
            _signalDispatcherBuffer = new byte[_maxSignalSize];

            _client = new NetManager(this)
            {
                AutoRecycle = true,
                EnableStatistics = true
            };
            _client.Start();

            _transportThreadLoopCts = new CancellationTokenSource();
            _transportThread = new Thread(RunTransportThreadLoop)
            {
                Name = $"{nameof(LiteNetLibTransport)}",
                IsBackground = true,
            };
            _transportThread.Start();
        }

        public void Dispose()
        {
            Disconnect();

            _transportThreadLoopCts?.Cancel();
            _transportThreadLoopCts?.Dispose();

            _client.Stop();

            DebugLogger.Log($"[{nameof(LiteNetLibTransport)}] Disposed.");
        }

        public async Task<bool> ConnectAsync<T>(T connectParameters, CancellationToken cancellationToken = default) where T : IConnectParameters
        {
            if (_connected || _connecting) return await _connectionTcs.Task;

            _connectionTcs = new TaskCompletionSource<bool>();
            _connecting = true;

            try
            {
                if (connectParameters is LiteNetLibConnectParameters LiteNetLibConnectParameters)
                {
                    _serverAddress = LiteNetLibConnectParameters.ServerAddress;
                    _serverPort = LiteNetLibConnectParameters.ServerPort;
                    // _connectionKey = LiteNetLibConnectParameters.ConnectionKey;
                }
                else
                {
                    throw new ArgumentException($"Invalid type of {nameof(connectParameters)}");
                }

                _peer = _client.Connect(_serverAddress, _serverPort, _connectionKey);

                await _connectionTcs.Task;
            }
            catch (Exception e)
            {
                _connected = false;
                _connectionTcs.SetResult(false);
                throw;
            }
            finally
            {
                _connecting = false;
            }

            return await _connectionTcs.Task;
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            Disconnect();
        }

        public void Disconnect()
        {
            if (!_connected) return;
            _peer.Disconnect();
            _connected = false;
        }

        public void Send(ArraySegment<byte> data, SendOptions sendOptions, uint[] destinationClientIds = null)
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

                    try
                    {
                        OnIncomingSignalDequeued?.Invoke(_incomingSignalsBuffer.Slice(0, bufferLength));
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

        public void EnqueueOutgoingSignal(ReadOnlySpan<byte> data, SendOptions sendOptions)
        {
            var dispatchRequest = new OutgoingSignalDispatchRequest
            {
                BufferLength = data.Length,
                Reliable = sendOptions.Reliable,
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
            DebugLogger.Log($"[{nameof(LiteNetLibTransport)}] Transport thread loop started. @Thread[{Thread.CurrentThread.ManagedThreadId}]");

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

            DebugLogger.Log($"[{nameof(LiteNetLibTransport)}] Transport thread loop exited. @Thread[{Thread.CurrentThread.ManagedThreadId}]");
        }

        void PollEvent()
        {
            _client.PollEvents();
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

                    _peer.Send(data, deliveryMethod);
                }
            }
        }

        uint GetClientId(NetPeer peer) => (uint)(peer.Id + 1);

        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
        }

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            DebugLogger.Log($"[{nameof(LiteNetLibTransport)}] OnPeerConnected - ClientId: {GetClientId(peer)}");
            _connected = true;
            _connectionTcs.SetResult(true);
            OnConnected?.Invoke();
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            DebugLogger.Log($"[{nameof(LiteNetLibTransport)}] Peer disconnected. Reason: {disconnectInfo.Reason}");
            OnDisconnected?.Invoke();
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
            DebugLogger.LogError($"[{nameof(LiteNetLibTransport)}] OnNetworkError. SocketErrorCode: {socketErrorCode}");
        }

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            DebugLogger.Log($"[{nameof(LiteNetLibTransport)}] OnNetworkReceiveUnconnect");
        }

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            // DebugLogger.Log($"<color=orange>[{nameof(LiteNetLibTransport)}] OnNetworkLatencyUpdate</color>");
        }
    }
}

// #endif