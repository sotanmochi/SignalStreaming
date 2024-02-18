using System;
using MessagePack;
using SignalStreaming.Infrastructure.ENet;
using UnityEngine;

namespace SignalStreaming.Samples.ENetSample
{
    public class SampleClient : MonoBehaviour
    {
        [SerializeField] string _serverAddress = "localhost";
        [SerializeField] ushort _port = 3333;
        [SerializeField] string _connectionKey = "SignalStreaming";

        ISignalStreamingClient _streamingClient;
        ISignalTransport _transport;
        IConnectParameters _connectParameters;

        void Awake()
        {
            _transport = new ENetTransport(useAnotherThread: true, targetFrameRate: 60, isBackground: true);

            _transport.OnConnected += () => Debug.Log($"[{nameof(SampleClient)}] TransportConnected");
            _transport.OnDisconnected += () => Debug.Log($"[{nameof(SampleClient)}] TransportDisconnected");
            _transport.OnDataReceived += (payload) => Debug.Log($"[{nameof(SampleClient)}] TransportDataReceived - Payload.Length: {payload.Count}");

            _connectParameters = new ENetConnectParameters()
            {
                ConnectionRequestData = System.Text.Encoding.UTF8.GetBytes(_connectionKey),
                ServerAddress = _serverAddress,
                ServerPort = _port
            };

            _streamingClient = new SignalStreamingClient(_transport);
            _streamingClient.OnConnected += OnConnected;
            _streamingClient.OnDisconnected += OnDisconnected;
            _streamingClient.OnDataReceived += OnDataReceived;
        }

        async void Start()
        {
            Debug.Log($"[{nameof(SampleClient)}] StreamingClient is connecting...");
            var connected = await _streamingClient.ConnectAsync(_connectParameters);
            Debug.Log($"[{nameof(SampleClient)}] StreamingClient.IsConnected: {connected}");
        }

        void Update()
        {
            var sendOptions = new SendOptions(StreamingType.All, reliable: true);
            _streamingClient.Send(messageId: 0, data: "Hello, world!", sendOptions);
        }

        void OnDestroy()
        {
            _streamingClient.OnConnected -= OnConnected;
            _streamingClient.OnDisconnected -= OnDisconnected;
            _streamingClient.OnDataReceived -= OnDataReceived;

            _streamingClient.Dispose();
            _transport.Dispose();
        }

        void OnConnected(uint clientId)
        {
            Debug.Log($"[{nameof(SampleClient)}] Connected - ClientId: {clientId}");
        }

        void OnDisconnected(string reason)
        {
            Debug.Log($"[{nameof(SampleClient)}] Disconnected - Reason: {reason}");
        }

        void OnDataReceived(int messageId, uint senderClientId, long originTimestamp, long transmitTimestamp, ReadOnlyMemory<byte> payload)
        {
            var originDateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(originTimestamp).ToString("MM/dd/yyyy hh:mm:ss.fff tt");
            var transmitDateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(transmitTimestamp).ToString("MM/dd/yyyy hh:mm:ss.fff tt");

            Debug.Log($"[{nameof(SampleClient)}] Received data sent from client[{senderClientId}]. " +
                $"Message ID: {messageId}, Payload.Length: {payload.Length}, " +
                $"OriginTimestamp: {originDateTimeOffset}, TransmitTimestamp: {transmitDateTimeOffset}");

            if (messageId == 0)
            {
                var text = MessagePackSerializer.Deserialize<string>(payload);
                Debug.Log($"<color=yello>[{nameof(SampleClient)}] Received message: {text}</color>");
            }
        }
    }
}
