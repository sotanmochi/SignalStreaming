#if ENET_CSHARP

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ENet;
using Event = ENet.Event;
using EventType = ENet.EventType;
using DebugLogger = SignalStreaming.DevelopmentOnlyLogger;

namespace SignalStreaming.Infrastructure.ENet
{
    /// <summary>
    /// Client implementation of ENet-CSharp
    /// </summary>
    public sealed class ENetTransport : ISignalTransport
    {
        static readonly double TimestampsToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        readonly Thread _receiverLoopThread;
        readonly CancellationTokenSource _receiverLoopCts;
        readonly int _targetFrameTimeMilliseconds;

        byte[] _buffer; // TODO: Ring buffer and dequeue thread

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
        public event Action<ArraySegment<byte>> OnDataReceived;

        public bool IsConnected => _connected;

        public ENetTransport(bool useAnotherThread, int targetFrameRate, bool isBackground)
        {
            _buffer = new byte[1024 * 4];

            Library.Initialize();
            _address = new Address();
            _client = new Host();
            _client.Create();

            if (useAnotherThread)
            {
                _targetFrameTimeMilliseconds = (int)(1000 / (double)targetFrameRate);

                _receiverLoopCts = new CancellationTokenSource();
                _receiverLoopThread = new Thread(ReceiverLoop)
                {
                    Name = $"{nameof(ENetTransport)}",
                    IsBackground = isBackground,
                };

                _receiverLoopThread.Start();

                // TODO: Dequeue thread
            }
        }

        public void Dispose()
        {
            Disconnect();

            _receiverLoopCts?.Cancel();
            _receiverLoopCts?.Dispose();

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

        // TODO: Ring buffer and dequeue thread

        public void PollEvent()
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
                        var length = netEvent.Packet.Length;
                        var buffer = (length <= _buffer.Length) ? _buffer : /* Temporary buffer */ new byte[length];
                        Marshal.Copy(netEvent.Packet.Data, buffer, 0, length);
                        netEvent.Packet.Dispose();
                        // TODO: Ring buffer and dequeue thread
                        OnDataReceived?.Invoke(new ArraySegment<byte>(buffer, 0, length));
                        break;
                }
            }

            _client.Flush();
        }

        void ReceiverLoop()
        {
            while (!_receiverLoopCts.IsCancellationRequested)
            {
                var begin = Stopwatch.GetTimestamp();

                PollEvent();

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
    }
}

#endif