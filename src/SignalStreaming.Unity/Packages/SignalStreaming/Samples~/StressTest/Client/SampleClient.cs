using System;
using System.Diagnostics;
using System.Buffers;
using System.Threading;
using MessagePack;
using Newtonsoft.Json;
using SignalStreaming.Infrastructure.ENet;
using SignalStreaming.Infrastructure.LiteNetLib;
using UnityEngine;
using Button = UnityEngine.UI.Button;
using Text = UnityEngine.UI.Text;
using Debug = UnityEngine.Debug;

namespace SignalStreaming.Samples.StressTest
{
    public class SampleClient : MonoBehaviour
    {
        [SerializeField] AppSettings _appSettings;
        [SerializeField] string _serverAddress = "localhost";
        [SerializeField] ushort _port = 3333;
        [SerializeField] string _connectionKey = "SignalStreaming";
        [SerializeField] string _groupId = "01HP8DMTNKAVNQDWCBMG9NWG8S";

        [SerializeField] PlayerMoveSystem _playerMoveSystem;

        [SerializeField] Button _connectionButton;
        [SerializeField] Text _connectionButtonText;
        [SerializeField] Button _transmissionButton;
        [SerializeField] Text _transmissionButtonText;
        [SerializeField] Button _resetButton;

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

        uint _receivedSignalCount1;
        uint _previousMeasuredSignalCount1;
        float _receivedSignalsPerSecond1;

        uint _receivedSignalCount2;
        uint _previousMeasuredSignalCount2;
        float _receivedSignalsPerSecond2;

        uint _receivedSignalCount3;
        uint _previousMeasuredSignalCount3;
        float _receivedSignalsPerSecond3;

        ISignalStreamingClient _streamingClient;
        ISignalTransport _transport;
        LiteNetLibConnectParameters _connectParameters;

        PlayerMoveController _localPlayerMoveController;
        ColorType _localPlayerColorType;
        Color _localPlayerColor;
        bool _colorUpdated;
        float _rainbowHueOffset;
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

            _connectParameters = new LiteNetLibConnectParameters()
            {
                ConnectionRequestData = System.Text.Encoding.UTF8.GetBytes(_connectionKey),
                ServerAddress = _serverAddress,
                ServerPort = _port
            };

            _connectionButtonText.text = "Connect to server";
            _connectionButton.onClick.AddListener(() =>
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

            _transmissionButtonText.text = "Start transmission";
            _transmissionButton.onClick.AddListener(() =>
            {
                _transmissionEnabled = !_transmissionEnabled;
                _transmissionButtonText.text = _transmissionEnabled ? "Stop transmission" : "Start transmission";
            });

            _resetButton.onClick.AddListener(() =>
            {
                _receivedSignalCount = 0;
                _previousMeasuredSignalCount = 0;
                _receivedSignalsPerSecond = 0;
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
            _streamingClient = new SignalStreamingClient(_transport);
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

        async void Start()
        {
        }

        void Update()
        {
            _transport.DequeueIncomingSignals();

            _receivedSignalCountText.text = $"{_receivedSignalCount}";
            _receivedSignalCountText1.text = $"{_receivedSignalCount1}";
            _receivedSignalCountText2.text = $"{_receivedSignalCount2}";
            _receivedSignalCountText3.text = $"{_receivedSignalCount3}";

            var currentTimeMilliseconds = _stopwatch.ElapsedMilliseconds;
            if (currentTimeMilliseconds - _previousMeasuredTimeMilliseconds > 1000)
            {
                var deltaTime = (currentTimeMilliseconds - _previousMeasuredTimeMilliseconds) / 1000f;
                _receivedSignalsPerSecond = (_receivedSignalCount - _previousMeasuredSignalCount) / deltaTime;
                _receivedSignalsPerSecond1 = (_receivedSignalCount1 - _previousMeasuredSignalCount1) / deltaTime;
                _receivedSignalsPerSecond2 = (_receivedSignalCount2 - _previousMeasuredSignalCount2) / deltaTime;
                _receivedSignalsPerSecond3 = (_receivedSignalCount3 - _previousMeasuredSignalCount3) / deltaTime;

                _previousMeasuredTimeMilliseconds = currentTimeMilliseconds;
                _previousMeasuredSignalCount = _receivedSignalCount;
                _previousMeasuredSignalCount1 = _receivedSignalCount1;
                _previousMeasuredSignalCount2 = _receivedSignalCount2;
                _previousMeasuredSignalCount3 = _receivedSignalCount3;

                _signalsPerSecondText.text = $"{_receivedSignalsPerSecond:F2} [signals/sec]";
                _signalsPerSecondText1.text = $"{_receivedSignalsPerSecond1:F2} [signals/sec]";
                _signalsPerSecondText2.text = $"{_receivedSignalsPerSecond2:F2} [signals/sec]";
                _signalsPerSecondText3.text = $"{_receivedSignalsPerSecond3:F2} [signals/sec]";
            }

            if (_transmissionEnabled && _localPlayerMoveController != null)
            {
                var sendOptions = new SendOptions(StreamingType.All, reliable: true);

                var position = _localPlayerMoveController.transform.position;
                var rotation = _localPlayerMoveController.transform.rotation;

                _streamingClient.Send((int)SignalType.PlayerObjectRotation, rotation, sendOptions);
                _streamingClient.Send((int)SignalType.PlayerObjectPosition, position, sendOptions);
            }

            if (_localPlayerColorType == ColorType.Rainbow)
            {
                _rainbowHueOffset += Time.deltaTime * 0.1f;
                _rainbowHueOffset %= 1f;
                _localPlayerColor = Color.HSVToRGB(_rainbowHueOffset, 1f, 1f);
                _playerMoveSystem.UpdateColor(_clientId, _localPlayerColor);
                _colorUpdated = true;
            }

            if (_transmissionEnabled && _colorUpdated)
            {
                var sendOptions = new SendOptions(StreamingType.All, reliable: true);
                _streamingClient.Send((int)SignalType.PlayerObjectColor, _localPlayerColor, sendOptions);
                _colorUpdated = false;
            }
        }

        Color GetColor(int colorType) => colorType switch
        {
            1 => Color.red,
            2 => Color.green,
            3 => Color.blue,
            4 => Color.cyan,
            5 => Color.magenta,
            6 => Color.yellow,
            _ => Color.white,
        };

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

        void OnConnected(uint clientId)
        {
            Debug.Log($"[{nameof(SampleClient)}] Connected - ClientId: {clientId}");
            _clientId = clientId;
            _playerMoveSystem.TryGetOrAdd(clientId, out _localPlayerMoveController);
            _playerMoveSystem.EnableAutopilot(clientId, true);

            _localPlayerColorType = ColorType.Random;

            _localPlayerColor = GetColor(UnityEngine.Random.Range(1, 7));
            _playerMoveSystem.UpdateColor(_clientId, _localPlayerColor);
            _colorUpdated = true;

            Debug.Log($"<color=cyan>[{nameof(SampleClient)}] Update color: {_localPlayerColor}, Type: {_localPlayerColorType}, Updated: {_colorUpdated}</color>");
        }

        void OnDisconnected(string reason)
        {
            Debug.Log($"[{nameof(SampleClient)}] Disconnected - Reason: {reason}");
            _playerMoveSystem.UpdateColor(_clientId, Color.red);
        }

        void OnIncomingSignalDequeued(int messageId, uint senderClientId, long originTimestamp, long transmitTimestamp, ReadOnlySequence<byte> payload)
        {
            _receivedSignalCount++;
            if (messageId == (int)SignalType.ChangeStressTestState)
            {
                if (senderClientId == _clientId) return;

                var state = MessagePackSerializer.Deserialize<StressTestState>(in payload);
                if (state == StressTestState.AllSignalEmittersDisabled)
                {
                    _transmissionEnabled = false;
                    _transmissionButtonText.text = "Start transmission";
                }
                else if (state == StressTestState.AllSignalEmittersEnabled)
                {
                    _transmissionEnabled = true;
                    _transmissionButtonText.text = "Stop transmission";
                }
            }
            else if (messageId == (int)SignalType.ChangeColor)
            {
                if (senderClientId == _clientId) return;

                _localPlayerColorType = MessagePackSerializer.Deserialize<ColorType>(in payload);
                
                if (_localPlayerColorType == ColorType.Random)
                {
                    _localPlayerColor = GetColor(UnityEngine.Random.Range(1, 7));
                }
                else if (_localPlayerColorType == ColorType.Rainbow)
                {
                    _rainbowHueOffset = 0f;
                    _localPlayerColor = Color.HSVToRGB(_rainbowHueOffset, 1f, 1f);
                }
                else
                {
                    _localPlayerColor = GetColor((int)_localPlayerColorType);
                }

                _playerMoveSystem.UpdateColor(_clientId, _localPlayerColor);
                _colorUpdated = true;
                UnityEngine.Debug.Log(string.Format("<color=cyan>[{0}] Update color: {1}, Type: {2}, Updated: {3}</color>", "SampleClient", _localPlayerColor, _localPlayerColorType, _colorUpdated));
            }
            else if (messageId == (int)SignalType.PlayerObjectColor)
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
