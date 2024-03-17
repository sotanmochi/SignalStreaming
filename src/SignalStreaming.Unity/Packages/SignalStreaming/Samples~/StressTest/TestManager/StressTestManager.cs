using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using MessagePack;
using Newtonsoft.Json;
using SignalStreaming;
using SignalStreaming.Infrastructure.LiteNetLib;
using SignalStreaming.Serialization;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace SignalStreaming.Samples.StressTest
{
    public class StressTestManager : MonoBehaviour
    {
        [SerializeField] string _serverAddress = "localhost";
        [SerializeField] ushort _port = 3333;
        [SerializeField] string _connectionKey = "SignalStreaming";
        [SerializeField] string _groupId = "01HP8DMTNKAVNQDWCBMG9NWG8S";

        [SerializeField] PlayerMoveSystem _playerMoveSystem;

        [SerializeField] Button _connectionButton;
        [SerializeField] Text _connectionButtonText;
        [SerializeField] Button _testStateSignalButton;
        [SerializeField] Dropdown _testStateDropdown;
        [SerializeField] Button _changeColorButton;
        [SerializeField] Dropdown _changeColorDropdown;
        [SerializeField] Button _resetButton;

        [SerializeField] Text _receivedMegaBytesText;
        [SerializeField] Text _receivedMegaBytesPerSecondText;
        [SerializeField] Text _receivedMegaBitsPerSecondText;

        [SerializeField] Text _receivedSignalCountText;
        [SerializeField] Text _signalsPerSecondText;
        [SerializeField] Text _receivedSignalCountText1;
        [SerializeField] Text _signalsPerSecondText1;
        [SerializeField] Text _receivedSignalCountText2;
        [SerializeField] Text _signalsPerSecondText2;
        [SerializeField] Text _receivedSignalCountText3;
        [SerializeField] Text _signalsPerSecondText3;

        readonly Stopwatch _stopwatch = new();

        CancellationTokenSource _connectionCts;
        bool _transmissionEnabled;

        long _previousMeasuredTimeMilliseconds;
        uint _receivedSignalCount;
        uint _previousMeasuredSignalCount;
        float _receivedSignalsPerSecond;

        long _receivedBytes;
        long _previousMeasuredBytes;
        float _receivedBytesPerSecond;

        uint _receivedSignalCount1;
        uint _previousMeasuredSignalCount1;
        float _receivedSignalsPerSecond1;

        uint _receivedSignalCount2;
        uint _previousMeasuredSignalCount2;
        float _receivedSignalsPerSecond2;

        uint _receivedSignalCount3;
        uint _previousMeasuredSignalCount3;
        float _receivedSignalsPerSecond3;

        ISignalSerializer _signalSerializer;
        ISignalStreamingClient _streamingClient;
        ISignalTransport _transport;
        LiteNetLibConnectParameters _connectParameters;

        PlayerMoveController _localPlayerMoveController;
        uint _clientId;

        void Awake()
        {
            Application.targetFrameRate = 60;
            _stopwatch.Start();

            var appSettingsFilePath = $"{Application.streamingAssetsPath}/appsettings.json";
            var appSettings = JsonConvert.DeserializeObject<AppSettings>(System.IO.File.ReadAllText(appSettingsFilePath));

            Debug.Log($"<color=cyan>[{nameof(StressTestManager)}] ServerAddress: {appSettings.ServerAddress}, Port: {appSettings.Port}, ConnectionKey: {appSettings.ConnectionKey}, GroupId: {appSettings.GroupId}</color>");

            _serverAddress = appSettings.ServerAddress;
            _port = appSettings.Port;
            _connectionKey = appSettings.ConnectionKey;
            _groupId = appSettings.GroupId;

            _testStateDropdown.ClearOptions();
            _testStateDropdown.AddOptions(new List<string>
            {
                "All Signal Emitter Disabled",
                "All Signal Emitter Enabled",
            });
            _testStateDropdown.value = 0;
            _testStateDropdown.RefreshShownValue();

            _testStateSignalButton.onClick.AddListener(() =>
            {
                var value = (StressTestState)_testStateDropdown.value;
                var sendOptions = new SendOptions(StreamingType.All, reliable: true);
                _streamingClient.Send((int)SignalType.ChangeStressTestState, value, sendOptions);
                Debug.Log($"<color=cyan>[{nameof(StressTestManager)}] Sent stress test state signal: {value}</color>");
            });

            _changeColorDropdown.ClearOptions();
            _changeColorDropdown.AddOptions(new List<string>
            {
                ColorType.Random.ToString(),
                ColorType.Red.ToString(),
                ColorType.Green.ToString(),
                ColorType.Blue.ToString(),
                ColorType.Cyan.ToString(),
                ColorType.Magenta.ToString(),
                ColorType.Yellow.ToString(),
                ColorType.Rainbow.ToString()
            });
            _changeColorDropdown.value = 0;
            _changeColorDropdown.RefreshShownValue();

            _changeColorButton.onClick.AddListener(() =>
            {
                var value = (ColorType)_changeColorDropdown.value;
                var sendOptions = new SendOptions(StreamingType.All, reliable: true);
                _streamingClient.Send((int)SignalType.ChangeColor, value, sendOptions);
                Debug.Log($"<color=cyan>[{nameof(StressTestManager)}] Sent color change signal: {value}</color>");
            });

            _connectParameters = new LiteNetLibConnectParameters
            {
                ConnectionRequestData = Encoding.UTF8.GetBytes(_connectionKey),
                ServerAddress = _serverAddress,
                ServerPort = _port
            };

            _connectionButtonText.text = "Connect to server";
            _connectionButton.onClick.AddListener(delegate
            {
                if (_connectionCts != null)
                {
                    _connectionCts.Cancel();
                    _connectionCts.Dispose();
                    _connectionCts = null;
                    _connectionButtonText.text = "Connect to server";
                }
                else
                {
                    _connectionCts = new CancellationTokenSource();
                    ConnectAsync(_connectionCts.Token);
                    _connectionButtonText.text = "Cancel connection";
                }
            });

            _resetButton.onClick.AddListener(delegate
            {
                _receivedSignalsPerSecond = 0;
                _receivedBytes = 0;
                _previousMeasuredBytes = 0;
                _receivedBytesPerSecond = 0;

                _receivedSignalCount = 0;
                _previousMeasuredSignalCount = 0;

                _receivedSignalCount1 = 0;
                _previousMeasuredSignalCount1 = 0;
                _receivedSignalsPerSecond1 = 0;

                _receivedSignalCount2 = 0;
                _previousMeasuredSignalCount2 = 0;
                _receivedSignalsPerSecond2 = 0;

                _receivedSignalCount3 = 0;
                _previousMeasuredSignalCount3 = 0;
                _receivedSignalsPerSecond3 = 0;
            });

            _transport = new LiteNetLibTransport(targetFrameRate: 120);
            _signalSerializer = new SignalSerializer(MessagePackSerializer.DefaultOptions);
            _streamingClient = new SignalStreamingClient(_transport, _signalSerializer);
            _streamingClient.OnConnected += OnConnected;
            _streamingClient.OnDisconnected += OnDisconnected;
            _streamingClient.OnIncomingSignalDequeued += OnIncomingSignalDequeued;
        }

        void OnDestroy()
        {
            _streamingClient.OnConnected -= OnConnected;
            _streamingClient.OnDisconnected -= OnDisconnected;
            _streamingClient.OnIncomingSignalDequeued -= OnIncomingSignalDequeued;

            _streamingClient.Dispose();
            _transport.Dispose();
        }

        void Update()
        {
            _transport.DequeueIncomingSignals();

            // _receivedMegaBytesText.text = $"{_receivedBytes / 1000000f:F4} [MB]";
            _receivedMegaBytesText.text = $"{_receivedBytes} [Bytes]";
            _receivedSignalCountText.text = $"{_receivedSignalCount}";
            _receivedSignalCountText1.text = $"{_receivedSignalCount1}";
            _receivedSignalCountText2.text = $"{_receivedSignalCount2}";
            _receivedSignalCountText3.text = $"{_receivedSignalCount3}";

            var currentTimeMilliseconds = _stopwatch.ElapsedMilliseconds;
            if (currentTimeMilliseconds - _previousMeasuredTimeMilliseconds > 1000)
            {
                float deltaTime = (currentTimeMilliseconds - _previousMeasuredTimeMilliseconds) / 1000f;

                _receivedBytesPerSecond = (_receivedBytes - _previousMeasuredBytes) / deltaTime;
                _receivedSignalsPerSecond = (_receivedSignalCount - _previousMeasuredSignalCount) / deltaTime;
                _receivedSignalsPerSecond1 = (_receivedSignalCount1 - _previousMeasuredSignalCount1) / deltaTime;
                _receivedSignalsPerSecond2 = (_receivedSignalCount2 - _previousMeasuredSignalCount2) / deltaTime;
                _receivedSignalsPerSecond3 = (_receivedSignalCount3 - _previousMeasuredSignalCount3) / deltaTime;

                _previousMeasuredTimeMilliseconds = currentTimeMilliseconds;
                _previousMeasuredBytes = _receivedBytes;

                _previousMeasuredSignalCount = _receivedSignalCount;
                _previousMeasuredSignalCount1 = _receivedSignalCount1;
                _previousMeasuredSignalCount2 = _receivedSignalCount2;
                _previousMeasuredSignalCount3 = _receivedSignalCount3;

                // _receivedMegaBytesPerSecondText.text = $"{_receivedBytesPerSecond / 1000000f:F4} [MB/s]";
                // _receivedMegaBitsPerSecondText.text = $"{_receivedBytesPerSecond * 8f / 1000000f:F4} [Mbps]";
                _receivedMegaBytesPerSecondText.text = $"{_receivedBytesPerSecond} [Bytes/s]";
                _receivedMegaBitsPerSecondText.text = $"{_receivedBytesPerSecond * 8f} [bits/s]";
                _signalsPerSecondText.text = $"{_receivedSignalsPerSecond:F2} [signals/sec]";
                _signalsPerSecondText1.text = $"{_receivedSignalsPerSecond1:F2} [signals/sec]";
                _signalsPerSecondText2.text = $"{_receivedSignalsPerSecond2:F2} [signals/sec]";
                _signalsPerSecondText3.text = $"{_receivedSignalsPerSecond3:F2} [signals/sec]";
            }
        }

        async void ConnectAsync(CancellationToken cancellationToken)
        {
            Debug.Log($"[{nameof(SampleClient)}] Trying to connect to server... (Thread: {Thread.CurrentThread.ManagedThreadId})");

            var connected = false;
            var joined = false;

            _connectParameters.ConnectionRequestData = System.Text.Encoding.UTF8.GetBytes(_connectionKey);
            _connectParameters.ServerAddress = _serverAddress;
            _connectParameters.ServerPort = _port;
            connected = await _streamingClient.ConnectAsync(_connectParameters, cancellationToken);
            if (connected)
            {
                Debug.Log($"[{nameof(SampleClient)}] Connected to server. (Thread: {Thread.CurrentThread.ManagedThreadId})");
                Debug.Log($"[{nameof(SampleClient)}] Trying to join group... (Thread: {Thread.CurrentThread.ManagedThreadId})");

                joined = await _streamingClient.JoinGroupAsync(_groupId, cancellationToken);
                if (joined)
                {
                    Debug.Log($"[{nameof(SampleClient)}] Joined group: {_groupId} (Thread: {Thread.CurrentThread.ManagedThreadId})");
                }
                else
                {
                    Debug.Log($"[{nameof(SampleClient)}] Failed to join group: {_groupId} (Thread: {Thread.CurrentThread.ManagedThreadId})");
                }
            }
            else
            {
                Debug.Log($"[{nameof(SampleClient)}] Failed to connect. (Thread: {Thread.CurrentThread.ManagedThreadId})");
            }

            if (connected && joined)
            {
                _connectionButtonText.text = "Disconnect";
            }
            else
            {
                _connectionButtonText.text = "Connect to server";
            }
        }

        private void OnConnected(uint clientId)
        {
            Debug.Log($"[{nameof(SampleClient)}] Connected - ClientId: {clientId}");
            _clientId = clientId;
        }

        private void OnDisconnected(string reason)
        {
            Debug.Log($"[{nameof(SampleClient)}] Disconnected - Reason: {reason}");
        }

        private void OnIncomingSignalDequeued(int messageId, ReadOnlySequence<byte> payload, uint senderClientId)
        {
            _receivedSignalCount++;
            _receivedBytes = _transport.BytesReceived;

            if (messageId == (int)SignalType.PlayerObjectColor)
            {
                _receivedSignalCount1++;

                if (senderClientId == _clientId) return;

                var color = MessagePackSerializer.Deserialize<Color>(payload);
                _playerMoveSystem.UpdateColor(senderClientId, color);
            }
            else if (messageId == (int)SignalType.PlayerObjectPosition)
            {
                _receivedSignalCount2++;

                if (senderClientId == _clientId) return;

                var position = MessagePackSerializer.Deserialize<Vector3>(payload);
                _playerMoveSystem.UpdatePosition(senderClientId, position);
            }
            else if (messageId == (int)SignalType.PlayerObjectRotation)
            {
                _receivedSignalCount3++;

                if (senderClientId == _clientId) return;

                var rotation = MessagePackSerializer.Deserialize<Quaternion>(payload);
                _playerMoveSystem.UpdateRotation(senderClientId, rotation);
            }
        }
    }
}
