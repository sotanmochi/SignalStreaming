#if ENET_CSHARP

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SignalStreaming.Collections;
using ENet;
using DebugLogger = SignalStreaming.DevelopmentOnlyLogger;

namespace SignalStreaming.Infrastructure.ENet
{
    /// <summary>
    /// Client implementation of ENet-CSharp
    /// </summary>
    public sealed class ENetTransport : ISignalTransport
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

        Address _address;
        Host _client;
        Peer _peer;

        TaskCompletionSource<bool> _connectionTcs;
        bool _connected;
        bool _connecting;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<ReadOnlySequence<byte>> OnIncomingSignalDequeued;

        public bool IsConnected => _connected;

        public ENetTransport(bool useAnotherThread, int targetFrameRate, bool isBackground)
        {
            _incomingSignalsBuffer = new ConcurrentRingBuffer<byte>(1024 * 4 * 4096); // 16MB
            _outgoingSignalsBuffer = new ConcurrentRingBuffer<byte>(1024 * 4 * 4096); // 16MB
            _signalDispatcherBuffer = new byte[_maxSignalSize];

            Library.Initialize();
            _address = new Address();
            _client = new Host();
            _client.Create();

            // if (useAnotherThread)
            {
                _targetFrameTimeMilliseconds = (int)(1000 / (double)targetFrameRate);

                _transportThreadLoopCts = new CancellationTokenSource();
                _transportThread = new Thread(RunTransportThreadLoop)
                {
                    Name = $"{nameof(ENetTransport)}",
                    IsBackground = isBackground,
                };

                _transportThread.Start();
            }
        }

        public void Dispose()
        {
            Disconnect();

            _transportThreadLoopCts?.Cancel();
            _transportThreadLoopCts?.Dispose();

            _client.Flush();
            _client.Dispose();
            _client = null;

            Library.Deinitialize();

            DebugLogger.Log($"[{nameof(ENetTransportHub)}] Disposed.");
        }

        public async Task<bool> ConnectAsync<T>(T connectParameters, CancellationToken cancellationToken = default) where T : IConnectParameters
        {
            if (_connected || _connecting) return await _connectionTcs.Task;

            _connectionTcs = new TaskCompletionSource<bool>();
            _connecting = true;

            try
            {
                if (connectParameters is ENetConnectParameters enetConnectParameters)
                {
                    _serverAddress = enetConnectParameters.ServerAddress;
                    _serverPort = enetConnectParameters.ServerPort;
                }
                else
                {
                    throw new ArgumentException($"Invalid type of {nameof(connectParameters)}");
                }

                _address.SetHost(_serverAddress);
                _address.Port = _serverPort;
                _peer = _client.Connect(_address, channelLimit: 4);

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

        public async Task DisconnectAsync(CancellationToken cancellationToken = default) => Disconnect();

        public void Disconnect()
        {
            if (!_connected) return;

            // Requests a disconnection from a peer,
            // but only after all queued outgoing packets are sent.
            _peer.DisconnectLater(0);

            _connected = false;
        }

        public void Send(ArraySegment<byte> data, SendOptions sendOptions, uint[] destinationClientIds = null)
        {
            var flags = sendOptions.Reliable
                ? (PacketFlags.Reliable | PacketFlags.NoAllocate) // Reliable Sequenced
                : (PacketFlags.None | PacketFlags.NoAllocate); // Unreliable Sequenced

            var packet = default(Packet);
            packet.Create(data.Array, data.Count, flags);

            _peer.Send(channelID: 0, ref packet);            
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

        void PollEvent()
        {
            var polled = false;

            while (!polled)
            {
                if (_client.CheckEvents(out var netEvent) <= 0)
                {
                    if (_client.Service(0, out netEvent) <= 0) break;
                    polled = true;
                }

                switch (netEvent.Type)
                {
                    case EventType.None:
                        // No Operation
                        break;
                    case EventType.Connect:
                        _connected = true;
                        _connectionTcs.SetResult(true);
                        OnConnected?.Invoke();
                        break;
                    case EventType.Disconnect:
                        // _connectionTcs?.SetResult(false); // REVIEW
                        OnDisconnected?.Invoke();
                        break;
                    case EventType.Timeout:
                        // No Operation
                        // _connectionTcs?.SetResult(false); // REVIEW
                        break;
                    case EventType.Receive:
                        HandleReceiveEvent(netEvent);
                        break;
                }
            }

            _client.Flush();
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

                    _peer.Send(channelID: 0, ref packet);

                    signalCount--;
                }
            }
        }

        unsafe void HandleReceiveEvent(Event netEvent)
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
            });

            netEvent.Packet.Dispose();
        }

        void RunTransportThreadLoop()
        {
            DebugLogger.Log($"[{nameof(ENetTransport)}] Transport thread loop started. @Thread[{Thread.CurrentThread.ManagedThreadId}]");

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

            DebugLogger.Log($"[{nameof(ENetTransport)}] Transport thread loop exited. @Thread[{Thread.CurrentThread.ManagedThreadId}]");
        }
    }
}

#endif