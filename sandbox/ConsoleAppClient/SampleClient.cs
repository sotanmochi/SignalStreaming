using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using SignalStreaming;
using SignalStreaming.Infrastructure.ENet;

namespace SignalStreamingSamples.ConsoleAppClient
{
    public class SampleClient : IStartable, ITickable
    {
        public const uint MAX_CLIENT_COUNT = 4096;
        public uint[] ReceivedMessageCountByClientId = new uint[MAX_CLIENT_COUNT];
        public float[] AveragePayloadSizeByClientId = new float[MAX_CLIENT_COUNT];
        public int[] MaxPayloadSizeByClientId = new int[MAX_CLIENT_COUNT];
        public int[] TotalPayloadSizeByClientId = new int[MAX_CLIENT_COUNT];

        readonly string _connectionKey;
        readonly string _serverAddress;
        readonly ushort _port;
        readonly string _groupId;

        ISignalTransport _transport;
        ISignalStreamingClient _streamingClient;

        public uint FrameCount { get; private set; }

        public SampleClient(string serverAddress, ushort port, string groupId, string connectionKey)
        {
            _serverAddress = serverAddress;
            _port = port;
            _connectionKey = connectionKey;
            _groupId = groupId;

            // _transport = new ENetTransport(useAnotherThread: true, targetFrameRate: 60, isBackground: true);
            _transport = new ENetTransport(useAnotherThread: false, targetFrameRate: 60, isBackground: true);
            _transport.OnConnected += () => Console.WriteLine($"[{nameof(ConsoleAppClient)}] TransportConnected");
            _transport.OnDisconnected += () => Console.WriteLine($"[{nameof(ConsoleAppClient)}] TransportDisconnected");
            _transport.OnDataReceived += (payload) => Console.WriteLine($"[{nameof(ConsoleAppClient)}] TransportDataReceived - Payload.Length: {payload.Count} @Thread: {Environment.CurrentManagedThreadId}");

            _streamingClient = new SignalStreamingClient(_transport);
            _streamingClient.OnConnected += OnConnected;
            _streamingClient.OnDisconnected += OnDisconnected;
            _streamingClient.OnDataReceived += OnDataReceived;
            _streamingClient.OnGroupJoinResponseReceived += OnGroupJoinResponseReceived;
            _streamingClient.OnGroupLeaveResponseReceived += OnGroupLeaveResponseReceived;
        }

        public void Dispose()
        {
            _streamingClient.Dispose();
            _transport.Dispose();
        }

        public void Start()
        {
            StartAsync();
        }

        public void Tick()
        {
            FrameCount++;
            // Log($"[{nameof(ConsoleAppClient)}] Tick (Thread: {Thread.CurrentThread.ManagedThreadId})");
            // _streamingClient.Send(messageId: 0, data: "Hello, world!", new SendOptions(StreamingType.All, reliable: true));
            _streamingClient.Send(messageId: 0, data: "Hello, world!", new SendOptions(StreamingType.All, reliable: false));
            _transport.PollEvent();
        }

        async void StartAsync()
        {
            var connectParameters = new ENetConnectParameters()
            {
                ConnectionRequestData = System.Text.Encoding.UTF8.GetBytes(_connectionKey),
                ServerAddress = _serverAddress,
                ServerPort = _port
            };

            Log($"[{nameof(ConsoleAppClient)}] Trying to connect to server... (Thread: {Thread.CurrentThread.ManagedThreadId})");
            var connected = await _streamingClient.ConnectAsync(connectParameters);
            if (connected)
            {
                Log($"[{nameof(ConsoleAppClient)}] Connected to server. (Thread: {Thread.CurrentThread.ManagedThreadId})");

                Log($"[{nameof(ConsoleAppClient)}] Trying to join group... (Thread: {Thread.CurrentThread.ManagedThreadId})");
                var joined = await _streamingClient.JoinGroupAsync(_groupId);
                if (joined)
                {
                    Log($"[{nameof(ConsoleAppClient)}] Joined group: {_groupId} (Thread: {Thread.CurrentThread.ManagedThreadId})");
                }
                else
                {
                    Log($"[{nameof(ConsoleAppClient)}] Failed to join group: {_groupId} (Thread: {Thread.CurrentThread.ManagedThreadId})");
                }
            }
            else
            {
                Log($"[{nameof(ConsoleAppClient)}] Failed to connect. (Thread: {Thread.CurrentThread.ManagedThreadId})");
            }
        }

        void OnConnected(uint clientId)
        {
            Log($"[{nameof(ConsoleAppClient)}] Connected - ClientId: {clientId}");
        }

        void OnDisconnected(string reason)
        {
            Log($"[{nameof(ConsoleAppClient)}] Disconnected - Reason: {reason}");
        }

        void OnGroupJoinResponseReceived(GroupJoinResponse response)
        {
            Log($"[{nameof(ConsoleAppClient)}] ConnectionId : {response.ConnectionId}");
        }

        void OnGroupLeaveResponseReceived(GroupLeaveResponse response)
        {
            Log($"[{nameof(ConsoleAppClient)}] ConnectionId : {response.ConnectionId}");
        }

        void OnDataReceived(int messageId, uint senderClientId, long originTimestamp, long transmitTimestamp, ReadOnlyMemory<byte> payload)
        {
            var originDateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(originTimestamp).ToString("MM/dd/yyyy hh:mm:ss.fff tt");
            var transmitDateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(transmitTimestamp).ToString("MM/dd/yyyy hh:mm:ss.fff tt");

            if (messageId == 0)
            {
                ReceivedMessageCountByClientId[senderClientId]++;
                TotalPayloadSizeByClientId[senderClientId] += payload.Length;
                AveragePayloadSizeByClientId[senderClientId] = TotalPayloadSizeByClientId[senderClientId] / ReceivedMessageCountByClientId[senderClientId];
                MaxPayloadSizeByClientId[senderClientId] = Math.Max(MaxPayloadSizeByClientId[senderClientId], payload.Length);

                var text = MessagePackSerializer.Deserialize<string>(payload);
            }
        }

        void Log(object message)
        {
            Console.WriteLine(message);
        }
    }
}
