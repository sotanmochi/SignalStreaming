using System;
using System.Diagnostics;
using System.Buffers;
using System.Threading;
using MessagePack;
using Newtonsoft.Json;
using SignalStreaming.Transports;
using SignalStreaming.Transports.ENet;
using SignalStreaming.Transports.LiteNetLib;
using SignalStreaming.Quantization;
using SignalStreaming.Serialization;
using UnityEngine;
using Button = UnityEngine.UI.Button;
using Text = UnityEngine.UI.Text;
using Debug = UnityEngine.Debug;

namespace SignalStreaming.Sandbox.StressTest
{
    public class SampleClient : MonoBehaviour
    {
        [SerializeField] string _serverAddress = "localhost";
        [SerializeField] ushort _port = 3333;
        [SerializeField] string _connectionKey = "SignalStreaming";
        [SerializeField] string _groupId = "01HP8DMTNKAVNQDWCBMG9NWG8S";

        [SerializeField] PlayerMoveSystem _playerMoveSystem;
        [SerializeField] Animator _selfOwnedCharacterPrefab;
        [SerializeField] Animator _replicatedCharacterPrefab;

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
        [SerializeField] Text _receivedSignalCountText4;
        [SerializeField] Text _signalsPerSecondText4;

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

        uint _receivedSignalCount4;
        uint _previousMeasuredSignalCount4;
        float _receivedSignalsPerSecond4;

        BoundedRange[] _worldBounds = new BoundedRange[]
        {
            new BoundedRange(-64f, 64f, 0.001f), // X
            new BoundedRange(-16f, 48f, 0.001f), // Y (Height)
            new BoundedRange(-64f, 64f, 0.001f), // Z
        };

        ISignalStreamingClient _streamingClient;
        ISignalTransport _transport;
        TransportConnectionOptions _connectionOptions;

        CharacterRepository _characterRepository;
        CharacterPoseService _characterPoseService;

        PlayerMoveController _localPlayerMoveController;
        ColorType _localPlayerColorType;
        Color _localPlayerColor;
        bool _colorUpdated;
        float _playerColorHue;
        byte _quantizedHue;
        uint _clientId;
        bool _autoConnect;
        bool _useCharacter;

        void Awake()
        {
            Application.targetFrameRate = 60;
            _stopwatch.Start();

            if (!Application.isEditor)
            {
                var appSettingsFilePath = $"{Application.streamingAssetsPath}/appsettings.json";
                var appSettings = JsonConvert.DeserializeObject<AppSettings>(System.IO.File.ReadAllText(appSettingsFilePath));

                Debug.Log($"<color=cyan>[{nameof(StressTestManager)}] ServerAddress: {appSettings.ServerAddress}, Port: {appSettings.Port}, ConnectionKey: {appSettings.ConnectionKey}, GroupId: {appSettings.GroupId}</color>");

                _autoConnect = appSettings.AutoConnect;
                _useCharacter = appSettings.UseCharacter;
                _serverAddress = appSettings.ServerAddress;
                _port = appSettings.Port;
                _connectionKey = appSettings.ConnectionKey;
                _groupId = appSettings.GroupId;
            }

            _connectionOptions = new TransportConnectionOptions()
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
                _characterPoseService.SetEnableTransmission(_transmissionEnabled);
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
                _receivedSignalCount4 = 0;
                _previousMeasuredSignalCount4 = 0;
                _receivedSignalsPerSecond4 = 0;
            });

            SignalFormatterProvider.Register(new ColorTypeFormatter());
            SignalFormatterProvider.Register(new StressTestStateFormatter());

            _transport = new LiteNetLibTransport(targetFrameRate: 120);
            _streamingClient = new SignalStreamingClient(_transport);
            _streamingClient.OnConnected += OnConnected;
            _streamingClient.OnDisconnected += OnDisconnected;
            _streamingClient.OnIncomingSignalDequeued += OnIncomingSignalDequeued;

            _characterRepository = new(_worldBounds, musclePrecision: 0.001f, _selfOwnedCharacterPrefab, _replicatedCharacterPrefab);
            _characterPoseService = new(_characterRepository, _streamingClient);
            _characterPoseService.SetEnableSelfOwnedCharacter(_useCharacter);
        }

        void OnDestroy()
        {
            _streamingClient.OnConnected -= OnConnected;
            _streamingClient.OnDisconnected -= OnDisconnected;
            _streamingClient.OnIncomingSignalDequeued -= OnIncomingSignalDequeued;

            _streamingClient.Dispose();
            _transport.Dispose();

            _characterPoseService.Dispose();
        }

        void Start()
        {
            if (_autoConnect)
            {
                _connectionCts = new CancellationTokenSource();
                ConnectAsync(_connectionCts.Token);
                _connectionButtonText.text = "Cancel connection";
            }
        }

        void Update()
        {
            _transport.DequeueIncomingSignals();

            _receivedSignalCountText.text = $"{_receivedSignalCount}";
            _receivedSignalCountText1.text = $"{_receivedSignalCount1}";
            _receivedSignalCountText2.text = $"{_receivedSignalCount2}";
            _receivedSignalCountText3.text = $"{_receivedSignalCount3}";
            _receivedSignalCountText4.text = $"{_receivedSignalCount4}";

            var currentTimeMilliseconds = _stopwatch.ElapsedMilliseconds;
            if (currentTimeMilliseconds - _previousMeasuredTimeMilliseconds > 1000)
            {
                var deltaTime = (currentTimeMilliseconds - _previousMeasuredTimeMilliseconds) / 1000f;
                _receivedSignalsPerSecond = (_receivedSignalCount - _previousMeasuredSignalCount) / deltaTime;
                _receivedSignalsPerSecond1 = (_receivedSignalCount1 - _previousMeasuredSignalCount1) / deltaTime;
                _receivedSignalsPerSecond2 = (_receivedSignalCount2 - _previousMeasuredSignalCount2) / deltaTime;
                _receivedSignalsPerSecond3 = (_receivedSignalCount3 - _previousMeasuredSignalCount3) / deltaTime;
                _receivedSignalsPerSecond4 = (_receivedSignalCount4 - _previousMeasuredSignalCount4) / deltaTime;

                _previousMeasuredTimeMilliseconds = currentTimeMilliseconds;
                _previousMeasuredSignalCount = _receivedSignalCount;
                _previousMeasuredSignalCount1 = _receivedSignalCount1;
                _previousMeasuredSignalCount2 = _receivedSignalCount2;
                _previousMeasuredSignalCount3 = _receivedSignalCount3;
                _previousMeasuredSignalCount4 = _receivedSignalCount4;

                _signalsPerSecondText.text = $"{_receivedSignalsPerSecond:F2} [signals/sec]";
                _signalsPerSecondText1.text = $"{_receivedSignalsPerSecond1:F2} [signals/sec]";
                _signalsPerSecondText2.text = $"{_receivedSignalsPerSecond2:F2} [signals/sec]";
                _signalsPerSecondText3.text = $"{_receivedSignalsPerSecond3:F2} [signals/sec]";
                _signalsPerSecondText4.text = $"{_receivedSignalsPerSecond4:F2} [signals/sec]";
            }

            if (_transmissionEnabled && _localPlayerMoveController != null)
            {
                // var sendOptions = new SendOptions(StreamingType.All, reliable: true);
                var sendOptions = new SendOptions(StreamingType.All, reliable: false);

                var position = _localPlayerMoveController.transform.position;
                var rotation = _localPlayerMoveController.transform.rotation;
                // _streamingClient.Send((int)SignalType.PlayerObjectPosition, position, sendOptions);
                // _streamingClient.Send((int)SignalType.PlayerObjectRotation, rotation, sendOptions);

                var quantizedPosition = BoundedRange.Quantize(position, _worldBounds);
                var quantizedRotation = SmallestThree.Quantize(rotation);
                _streamingClient.Send((int)SignalType.PlayerObjectQuantizedPosition, quantizedPosition, sendOptions);
                _streamingClient.Send((int)SignalType.PlayerObjectQuantizedRotation, quantizedRotation, sendOptions);
            }

            if (_localPlayerColorType == ColorType.Rainbow)
            {
                _playerColorHue += Time.deltaTime * 0.1f;
                _playerColorHue %= 1f;
                _quantizedHue = (byte)(_playerColorHue * 255f);
                _localPlayerColor = Color.HSVToRGB(_playerColorHue, 1f, 1f);
                _playerMoveSystem.UpdateColor(_clientId, _localPlayerColor);
                _colorUpdated = true;
            }

            if (_transmissionEnabled && _colorUpdated)
            {
                var sendOptions = new SendOptions(StreamingType.All, reliable: true);
                _streamingClient.Send((int)SignalType.PlayerObjectColor, _quantizedHue, sendOptions);
                _colorUpdated = false;
            }
        }

        void LateUpdate()
        {
            _characterPoseService.LateTick();
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

            _connectionOptions.ConnectionRequestData = System.Text.Encoding.UTF8.GetBytes(_connectionKey);
            _connectionOptions.ServerAddress = _serverAddress;
            _connectionOptions.ServerPort = _port;
            connected = await _streamingClient.ConnectAsync(_connectionOptions, cancellationToken);
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

            if (!_useCharacter)
            {
                _playerMoveSystem.TryGetOrAdd(clientId, out _localPlayerMoveController);
                _playerMoveSystem.EnableAutopilot(clientId, true);

                _localPlayerColorType = ColorType.Rainbow;
                _playerColorHue += Time.deltaTime * 0.1f;
                _playerColorHue %= 1f;
                _quantizedHue = (byte)(_playerColorHue * 255f);
                _localPlayerColor = Color.HSVToRGB(_playerColorHue, 1f, 1f);

                _playerMoveSystem.UpdateColor(_clientId, _localPlayerColor);
                _colorUpdated = true;

                Debug.Log($"<color=cyan>[{nameof(SampleClient)}] Update color: {_localPlayerColor}, Type: {_localPlayerColorType}, Updated: {_colorUpdated}</color>");
            }
        }

        void OnDisconnected(string reason)
        {
            Debug.Log($"[{nameof(SampleClient)}] Disconnected - Reason: {reason}");
            _playerMoveSystem.UpdateColor(_clientId, Color.red);
        }

        void OnIncomingSignalDequeued(int messageId, ReadOnlySequence<byte> payload, uint senderClientId)
        {
            _receivedSignalCount++;
            if (messageId == (int)SignalType.ChangeStressTestState)
            {
                if (senderClientId == _clientId) return;

                var state = SignalSerializer.Deserialize<StressTestState>(in payload);
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

                _characterPoseService.SetEnableTransmission(_transmissionEnabled);
            }
            else if (messageId == (int)SignalType.ChangeColor)
            {
                if (senderClientId == _clientId) return;

                _localPlayerColorType = SignalSerializer.Deserialize<ColorType>(in payload);
                
                if (_localPlayerColorType == ColorType.Random)
                {
                    _localPlayerColor = GetColor(UnityEngine.Random.Range(1, 7));
                }
                else if (_localPlayerColorType == ColorType.Rainbow)
                {
                    _playerColorHue = 0f;
                    _localPlayerColor = Color.HSVToRGB(_playerColorHue, 1f, 1f);
                }
                else
                {
                    _localPlayerColor = GetColor((int)_localPlayerColorType);
                }

                _playerMoveSystem.UpdateColor(_clientId, _localPlayerColor);
                _colorUpdated = true;
                UnityEngine.Debug.Log(string.Format("<color=cyan>[{0}] Update color: {1}, Type: {2}, Updated: {3}</color>", "SampleClient", _localPlayerColor, _localPlayerColorType, _colorUpdated));
            }
            else if (messageId == (int)SignalType.QuantizedHumanoidPose)
            {
                _receivedSignalCount4++;
            }
            else if (messageId == (int)SignalType.PlayerObjectColor)
            {
                _receivedSignalCount1++;

                if (senderClientId == _clientId) return;

                var quantizedHue = SignalSerializer.Deserialize<byte>(payload);
                var color = Color.HSVToRGB(quantizedHue / 255f, 1f, 1f);
                _playerMoveSystem.UpdateColor(senderClientId, color);
            }
            else if (messageId == (int)SignalType.PlayerObjectPosition)
            {
                _receivedSignalCount2++;

                if (senderClientId == _clientId) return;

                var position = SignalSerializer.Deserialize<Vector3>(payload);
                _playerMoveSystem.UpdatePosition(senderClientId, position);
            }
            else if (messageId == (int)SignalType.PlayerObjectRotation)
            {
                _receivedSignalCount3++;

                if (senderClientId == _clientId) return;

                var rotation = SignalSerializer.Deserialize<Quaternion>(payload);
                _playerMoveSystem.UpdateRotation(senderClientId, rotation);
            }
            else if (messageId == (int)SignalType.PlayerObjectQuantizedPosition)
            {
                _receivedSignalCount2++;

                if (senderClientId == _clientId) return;

                var position = new Vector3();
                var quantizedPosition = SignalSerializer.Deserialize<QuantizedVector3>(payload);
                BoundedRange.DequantizeTo(ref position, quantizedPosition, _worldBounds);

                _playerMoveSystem.UpdatePosition(senderClientId, position);
            }
            else if (messageId == (int)SignalType.PlayerObjectQuantizedRotation)
            {
                _receivedSignalCount3++;

                if (senderClientId == _clientId) return;

                var rotation = new Quaternion();
                var quantizedRotation = SignalSerializer.Deserialize<QuantizedQuaternion>(payload);
                SmallestThree.DequantizeTo(ref rotation, quantizedRotation);

                _playerMoveSystem.UpdateRotation(senderClientId, rotation);
            }
        }
    }
}
