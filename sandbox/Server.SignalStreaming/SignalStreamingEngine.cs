using System;
using System.Buffers;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalStreaming;
using SignalStreaming.Transports;
using SignalStreaming.Transports.LiteNetLib;
using Sandbox.EngineLooper;

namespace Sandbox.Server.SignalStreaming
{
    public sealed class SignalStreamingEngine : IDisposable, IStartable, ITickable
    {
        readonly SignalStreamingOptions _options;
        readonly IFrameProvider _frameProvider;
        readonly ILogger<SignalStreamingEngine> _logger;
        readonly Stopwatch _stopwatch = new();

        SignalStreamingHub _streamingHub;
        ISignalTransportHub _transportHub;

        bool _disposed;

        // Metrics
        int _minFrameProcessingTimeMs = int.MaxValue;
        int _maxFrameProcessingTimeMs = int.MinValue;
        ulong _incomingSignalCount;
        ulong _lastObservedIncomingSignalCount;

        public SignalStreamingEngine(
            IOptions<SignalStreamingOptions> options,
            IFrameProvider frameProvider,
            ILogger<SignalStreamingEngine> logger) : this(options.Value, frameProvider, logger)
        {
        }

        public SignalStreamingEngine(
            SignalStreamingOptions options,
            IFrameProvider frameProvider,
            ILogger<SignalStreamingEngine> logger = null)
        {
            _options = options;
            _frameProvider = frameProvider;
            _logger = logger;

            _transportHub = new LiteNetLibTransportHub(options.Port, targetFrameRate: 120, maxGroups: 32);
            _streamingHub = new SignalStreamingHub(_transportHub);

            _streamingHub.OnClientConnectionRequested += OnClientConnectionRequested;
            _streamingHub.OnClientConnected += OnClientConnected;
            _streamingHub.OnClientDisconnected += OnClientDisconnected;
            _streamingHub.OnIncomingSignalDequeued += OnIncomingSignalDequeued;
            _streamingHub.OnGroupJoinRequestReceived += OnGroupJoinRequestReceived;
            _streamingHub.OnGroupLeaveRequestReceived += OnGroupLeaveRequestReceived;

            _streamingHub.TryAddGroup("01HP8DMTNKAVNQDWCBMG9NWG8S", "DevGroup", out var group);

            LogInfo($"Port: {_options.Port}");
            LogInfo($"Default group is added. GroupId: {group.Id}, GroupName: {group.Name}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _streamingHub.OnClientConnectionRequested -= OnClientConnectionRequested;
            _streamingHub.OnClientConnected -= OnClientConnected;
            _streamingHub.OnClientDisconnected -= OnClientDisconnected;
            _streamingHub.OnIncomingSignalDequeued -= OnIncomingSignalDequeued;
            _streamingHub.OnGroupJoinRequestReceived -= OnGroupJoinRequestReceived;
            _streamingHub.OnGroupLeaveRequestReceived -= OnGroupLeaveRequestReceived;

            _streamingHub.Dispose();
            _transportHub.Dispose();

            LogInfo("Disposed");
        }

        void IStartable.Start()
        {
            LogInfo("IStartable.Start");
            _stopwatch.Start();
            _transportHub.Start();
        }

        void ITickable.Tick()
        {
            _transportHub.DequeueIncomingSignals();

            // Metrics
            var frameProcessingTimeMs = (int)_frameProvider.LastFrameProcessingTimeMilliseconds;
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
        }

        void OnIncomingSignalDequeued(int signalId, ReadOnlySequence<byte> bytes, SendOptions sendOptions, uint sourceClientId)
        {
            _incomingSignalCount++;

            if (sendOptions.StreamingType == StreamingType.All)
            {
                if (!_streamingHub.TryGetGroupId(sourceClientId, out var groupId)) return;
                _streamingHub.BroadcastRawBytes(groupId, signalId, bytes, sendOptions.Reliable, sourceClientId);
            }
        }

        ClientConnectionResponse OnClientConnectionRequested(uint clientId, ClientConnectionRequest connectionRequest)
        {
            LogInfo($"Connection request from Client[{clientId}]");

            var requestConnectionKey = System.Text.Encoding.UTF8.GetString(connectionRequest.ConnectionKey);

            var approved = (requestConnectionKey == _options.ConnectionKey);
            var connectionId = approved ? Ulid.NewUlid().ToString() : string.Empty;
            var message = approved ? "Connection request is approved." : "Connection request is rejected.";

            if (approved)
            {
                LogInfo($"{message} Client[{clientId}].ConnectionId: {connectionId}");
            }
            else
            {
                LogInfo($"{message} Client[{clientId}].RequestConnectionKey: {requestConnectionKey}");
            }

            return new ClientConnectionResponse(approved, clientId, connectionId, message);
        }

        void OnClientConnected(uint clientId)
        {
            LogInfo($"Client connected. Client[{clientId}]");
        }

        void OnClientDisconnected(uint clientId)
        {
            LogInfo($"Client disconnected. Client[{clientId}]");
        }

        void OnGroupJoinRequestReceived(uint clientId, GroupJoinRequest groupJoinRequest)
        {
            var groupId = groupJoinRequest.GroupId;

            LogInfo($"Group join request is received. Client[{clientId}], GroupId: {groupId}");

            if (_streamingHub.TryGetGroup(groupId, out var group))
            {
                if (_streamingHub.TryAddClientToGroup(clientId, groupId))
                {
                    LogInfo($"Group join request is approved. Client[{clientId}], GroupId: {groupId}");
                    _streamingHub.Broadcast(
                        groupId,
                        signalId: (int)MessageType.GroupJoinResponse,
                        value: new GroupJoinResponse(requestApproved: true, group.Id, groupJoinRequest.ConnectionId),
                        reliable: true,
                        sourceClientId: clientId);
                }
                else
                {
                    LogInfo($"Group join request is rejected. Client[{clientId}], GroupId: {groupId}");
                    _streamingHub.Send(
                        signalId: (int)MessageType.GroupJoinResponse,
                        value: new GroupJoinResponse(requestApproved: false, group.Id, groupJoinRequest.ConnectionId),
                        reliable: true,
                        sourceClientId: 0, // Server
                        destinationClientId: clientId);
                }
            }
            else
            {
                LogInfo($"The group is not found. GroupId: {groupId}");
                _streamingHub.Send(
                    signalId: (int)MessageType.GroupJoinResponse, 
                    value: new GroupJoinResponse(requestApproved: false, groupId, groupJoinRequest.ConnectionId),
                    reliable: true,
                    sourceClientId: 0,
                    destinationClientId: clientId);
            }
        }

        void OnGroupLeaveRequestReceived(uint clientId, GroupLeaveRequest groupLeaveRequest)
        {
            var groupId = groupLeaveRequest.GroupId;

            LogInfo($"Group leave request is received. Client[{clientId}], GroupId: {groupId}");

            if (_streamingHub.TryGetGroup(groupId, out var group))
            {
                if (_streamingHub.TryRemoveClientFromGroup(clientId, groupId))
                {
                    LogInfo($"Group leave request is approved. Client[{clientId}], GroupId: {groupId}");
                    _streamingHub.Broadcast(
                        groupId,
                        signalId: (int)MessageType.GroupLeaveResponse,
                        value: new GroupLeaveResponse(requestApproved: true, group.Id, groupLeaveRequest.ConnectionId),
                        reliable: true,
                        sourceClientId: clientId);
                }
            }
        }

        void LogInfo(string message)
        {
            _logger?.LogInformation($"[{nameof(SignalStreamingEngine)}] {message}");
        }
    }
}