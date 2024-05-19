using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SignalStreaming;
using SignalStreaming.Quantization;
using SignalStreaming.Serialization;
using SignalStreaming.Transports;
using SignalStreaming.Transports.LiteNetLib;
using Sandbox.EngineLooper;

namespace Sandbox.StressTest.Client
{
    public sealed class SignalStreamingEngine : IDisposable, IStartable, ITickable
    {
        readonly IFrameProvider _frameProvider;
        readonly ILogger<SignalStreamingEngine> _logger;
        readonly Stopwatch _stopwatch = new();

        SignalStreamingClient _streamingClient;
        ISignalTransport _transport;

        bool _disposed;

        // Metrics
        int _minFrameProcessingTimeMs = int.MaxValue;
        int _maxFrameProcessingTimeMs = int.MinValue;
        ulong _incomingSignalCount;
        ulong _lastObservedIncomingSignalCount;

        bool _transmissionEnabled = true;

        BoundedRange[] _worldBounds = new BoundedRange[]
        {
            new BoundedRange(-64f, 64f, 0.001f), // X
            new BoundedRange(-16f, 48f, 0.001f), // Y (Height)
            new BoundedRange(-64f, 64f, 0.001f), // Z
        };

        ObjectPoseCalculator _poseCalculator = new();
        Random _random = new();

        bool _colorUpdated;
        ColorType _localPlayerColorType = ColorType.Rainbow;
        float _localPlayerColorHue;
        byte _quantizedColorHue;

        public SignalStreamingEngine(
            Looper engineLooper,
            IFrameProvider frameProvider,
            IFrameTimingObserver frameTimingObserver,
            ILogger<SignalStreamingEngine> logger)
        {
            _frameProvider = frameProvider;
            _logger = logger;

            _transport = new LiteNetLibTransport(targetFrameRate: 120);
            _streamingClient = new SignalStreamingClient(_transport);

            _streamingClient.OnConnected += OnConnected;
            _streamingClient.OnDisconnected += OnDisconnected;
            _streamingClient.OnIncomingSignalDequeued += OnIncomingSignalDequeued;

            engineLooper.Register(this);
            engineLooper.Register(frameTimingObserver);

            SignalFormatterProvider.Register(new ColorTypeFormatter());
            SignalFormatterProvider.Register(new StressTestStateFormatter());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _streamingClient.OnConnected -= OnConnected;
            _streamingClient.OnDisconnected -= OnDisconnected;
            _streamingClient.OnIncomingSignalDequeued -= OnIncomingSignalDequeued;

            _streamingClient.Dispose();
            _transport.Dispose();

            LogInfo("Disposed");
        }

        public async Task ConnectAsync(SignalStreamingOptions options, CancellationToken cancellationToken)
        {
            LogInfo($"Trying to connect to server... (Thread: {Thread.CurrentThread.ManagedThreadId})");

            var connected = false;
            var joined = false;

            var groupId = options.GroupId;
            var connectionOptions = new LiteNetLibConnectParameters()
            {
                ServerAddress = options.ServerAddress,
                ServerPort = options.ServerPort,
                ConnectionRequestData = System.Text.Encoding.UTF8.GetBytes(options.ConnectionKey),
            };

            LogInfo($"ServerAddress: {options.ServerAddress}, ServerPort: {options.ServerPort}, ConnectionKey: {options.ConnectionKey}, GroupId: {groupId}");
            connected = await _streamingClient.ConnectAsync(connectionOptions, cancellationToken);
            if (connected)
            {
                LogInfo($"Connected to server. (Thread: {Thread.CurrentThread.ManagedThreadId})");
                LogInfo($"Trying to join group... (Thread: {Thread.CurrentThread.ManagedThreadId})");

                joined = await _streamingClient.JoinGroupAsync(groupId, cancellationToken);
                if (joined)
                {
                    LogInfo($"Joined group: {groupId} (Thread: {Thread.CurrentThread.ManagedThreadId})");
                }
                else
                {
                    LogInfo($"Failed to join group: {groupId} (Thread: {Thread.CurrentThread.ManagedThreadId})");
                }
            }
            else
            {
                LogInfo($"Failed to connect. (Thread: {Thread.CurrentThread.ManagedThreadId})");
            }
        }

        void IStartable.Start()
        {
            LogInfo("IStartable.Start");
            _stopwatch.Start();
            _poseCalculator.Startup();
        }

        void ITickable.Tick()
        {
            _transport.DequeueIncomingSignals();

            // Metrics
            var frameProcessingTimeMs = _frameProvider.LastFrameProcessingTimeMilliseconds;
            if (frameProcessingTimeMs < _minFrameProcessingTimeMs) _minFrameProcessingTimeMs = frameProcessingTimeMs;
            if (frameProcessingTimeMs > _maxFrameProcessingTimeMs) _maxFrameProcessingTimeMs = frameProcessingTimeMs;

            // Log metrics every 60 seconds
            if (_stopwatch.ElapsedMilliseconds >= 60000)
            {
                var incomingSignalCountDiff = _incomingSignalCount - _lastObservedIncomingSignalCount;

                LogInfo($"Metrics snapshot (last 60 seconds) - MinFrameProcessingTime: {_minFrameProcessingTimeMs}[ms], MaxFrameProcessingTime: {_maxFrameProcessingTimeMs}[ms]");
                LogInfo($"Metrics snapshot (last 60 seconds) - IncomingSignalCount: {incomingSignalCountDiff}, IncomingSignalRate: {(incomingSignalCountDiff) / 60f}[signals/sec]");

                _minFrameProcessingTimeMs = int.MaxValue;
                _maxFrameProcessingTimeMs = int.MinValue;
                _lastObservedIncomingSignalCount = _incomingSignalCount;

                _stopwatch.Restart();
            }

            UpdateLocalPlayer();
            SendSignals();
        }

        void UpdateLocalPlayer()
        {
            _poseCalculator.Tick();

            if (_localPlayerColorType == ColorType.Rainbow)
            {
                var deltaTime = (float)_frameProvider.LastFrameDeltaTimeMilliseconds / 1000f;
                _localPlayerColorHue += deltaTime * 0.1f;
                _localPlayerColorHue %= 1f;
                _colorUpdated = true;
            }
        }

        void SendSignals()
        {
            if (_transmissionEnabled)
            {
                var sendOptions = new SendOptions(StreamingType.All, reliable: false);

                var position = _poseCalculator.Position.ToSystemNumericsVector3();
                var rotation = _poseCalculator.Rotation.ToSystemNumericsQuaternion();
                var quantizedPosition = BoundedRange.Quantize(position, _worldBounds);
                var quantizedRotation = SmallestThree.Quantize(rotation);

                _streamingClient.Send((int)SignalType.PlayerObjectQuantizedPosition, quantizedPosition, sendOptions);
                _streamingClient.Send((int)SignalType.PlayerObjectQuantizedRotation, quantizedRotation, sendOptions);
            }

            if (_transmissionEnabled && _colorUpdated)
            {
                var sendOptions = new SendOptions(StreamingType.All, reliable: false);
                _quantizedColorHue = (byte)(_localPlayerColorHue * 255f);
                _streamingClient.Send((int)SignalType.PlayerObjectColor, _quantizedColorHue, sendOptions);
                _colorUpdated = false;
            }
        }

        void OnIncomingSignalDequeued(int signalId, ReadOnlySequence<byte> bytes, uint sourceClientId)
        {
            _incomingSignalCount++;

            if (signalId == (int)SignalType.ChangeStressTestState)
            {
                var state = SignalSerializer.Deserialize<StressTestState>(in bytes);
                if (state == StressTestState.AllSignalEmittersDisabled)
                {
                    _transmissionEnabled = false;
                }
                else if (state == StressTestState.AllSignalEmittersEnabled)
                {
                    _transmissionEnabled = true;
                }
            }
            else if (signalId == (int)SignalType.ChangeColor)
            {
                _localPlayerColorType = SignalSerializer.Deserialize<ColorType>(in bytes);
                
                if (_localPlayerColorType == ColorType.Random)
                {
                    _localPlayerColorHue = GetColorHue(_random.Next(1, 7));
                }
                else if (_localPlayerColorType == ColorType.Rainbow)
                {
                    _localPlayerColorHue = 0f;
                }
                else
                {
                    _localPlayerColorHue = GetColorHue((int)_localPlayerColorType);
                }

                _colorUpdated = true;

                LogInfo($"Color changed to: {_localPlayerColorType}, Hue: {_localPlayerColorHue}");
            }
        }

        float GetColorHue(int colorType) => colorType switch
        {
            1 => 0f, // Red
            2 => 0.333f, // Green
            3 => 0.666f, // Blue
            4 => 0.5f, // Cyan
            5 => 0.833f, // Magenta
            6 => 0.167f, // Yellow
            _ => 0f,
        };

        void OnConnected(uint clientId)
        {
            LogInfo($"Client connected. Client[{clientId}]");
        }

        void OnDisconnected(string reason)
        {
            LogInfo($"Client Disconnected - Reason: {reason}");
        }

        void LogInfo(string message)
        {
            _logger?.LogInformation(message);
        }
    }
}