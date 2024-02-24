using System;
using System.Buffers;
using System.Diagnostics;
using MessagePack;
using SignalStreaming.Infrastructure.ENet;
using UnityEngine;
using Text = UnityEngine.UI.Text;
using Debug = UnityEngine.Debug;

namespace SignalStreaming.Samples.ENetSample
{
    public class SampleServer : MonoBehaviour
    {
        [SerializeField] ushort _port = 3333;
        [SerializeField] string _connectionKey = "SignalStreaming";
        [SerializeField] string _groupId = "01HP8DMTNKAVNQDWCBMG9NWG8S";
        [SerializeField] Text _receivedSignalCountText;
        [SerializeField] Text _signalsPerSecondText;
        [SerializeField] Text _latestReceivedMessageText;
        [SerializeField] Text _senderClientIdText;

        uint _receivedSignalCount;
        float _receivedSignalsPerSecond;
        uint _previousMeasuredSignalCount;
        long _previousMeasuredTimeMilliseconds;
        Stopwatch _stopwatch = new();

        ISignalStreamingHub _streamingHub;
        ISignalTransportHub _transportHub;

        void Awake()
        {
            Application.targetFrameRate = 60;
            _stopwatch.Start();

            _transportHub = new ENetTransportHub(_port, useAnotherThread: true, targetFrameRate: 120, isBackground: true);
            // _transportHub = new ENetTransportHub(_port, useAnotherThread: false, targetFrameRate: 60, isBackground: true);
            _streamingHub = new SignalStreamingHub(_transportHub);

            _streamingHub.OnClientConnectionRequested += OnClientConnectionRequested;
            _streamingHub.OnClientConnected += OnConnected;
            _streamingHub.OnClientDisconnected += OnDisconnected;
            _streamingHub.OnIncomingSignalDequeued += OnIncomingSignalDequeued;
            _streamingHub.OnGroupJoinRequestReceived += OnGroupJoinRequestReceived;
            _streamingHub.OnGroupLeaveRequestReceived += OnGroupLeaveRequestReceived;

            _receivedSignalCountText.text = $"{_receivedSignalCount}";
        }

        void Start()
        {
            _transportHub.Start();
            _transportHub.TryAddGroup(_groupId, "DevGroup", out var group);
        }

        void Update()
        {
            var currentTimeMilliseconds = _stopwatch.ElapsedMilliseconds;
            if (currentTimeMilliseconds - _previousMeasuredTimeMilliseconds > 1000)
            {
                var deltaTime = (currentTimeMilliseconds - _previousMeasuredTimeMilliseconds) / 1000f;
                _receivedSignalsPerSecond = (_receivedSignalCount - _previousMeasuredSignalCount) / deltaTime;

                _previousMeasuredSignalCount = _receivedSignalCount;
                _previousMeasuredTimeMilliseconds = currentTimeMilliseconds;

                _signalsPerSecondText.text = $"{_receivedSignalsPerSecond:F2} [signals/sec]";
            }

            _transportHub.DequeueIncomingSignals();
        }

        void OnDestroy()
        {
            _streamingHub.OnClientConnectionRequested -= OnClientConnectionRequested;
            _streamingHub.OnClientConnected -= OnConnected;
            _streamingHub.OnClientDisconnected -= OnDisconnected;
            _streamingHub.OnIncomingSignalDequeued -= OnIncomingSignalDequeued;
            _streamingHub.OnGroupJoinRequestReceived -= OnGroupJoinRequestReceived;
            _streamingHub.OnGroupLeaveRequestReceived -= OnGroupLeaveRequestReceived;

            _streamingHub.Dispose();
            _transportHub.Dispose();
        }

        ClientConnectionResponse OnClientConnectionRequested(uint clientId, ClientConnectionRequest connectionRequest)
        {
            var connectionKey = System.Text.Encoding.UTF8.GetString(connectionRequest.ConnectionKey);

            var approved = (connectionKey == _connectionKey);
            var connectionId = Ulid.NewUlid().ToString();
            var message = approved
                ? "Connection request is approved."
                : "Connection request is rejected. Invalid connection request data.";

            return new ClientConnectionResponse(approved, clientId, connectionId, message);
        }

        void OnConnected(uint clientId)
        {
            Debug.Log($"[{nameof(SampleServer)}] Connected - Client[{clientId}]");
        }

        void OnDisconnected(uint clientId)
        {
            Debug.Log($"[{nameof(SampleServer)}] Disconnected - Client[{clientId}]");
        }

        void OnGroupJoinRequestReceived(uint clientId, GroupJoinRequest groupJoinRequest)
        {
            var groupId = groupJoinRequest.GroupId;

            Debug.Log($"[{nameof(SampleServer)}] Group join request received - Client[{clientId}], GroupId: {groupId}");

            if (_streamingHub.TryGetGroup(groupId, out var group))
            {
                if (_streamingHub.TryAddClientToGroup(clientId, groupId))
                {
                    Debug.Log($"<color=lime>[{nameof(SampleServer)}] AddClientToGroup - GroupId: {group.Id}, GroupName: {group.Name}, ClientId: {clientId}</color>");
                    _streamingHub.Broadcast(groupId, (int)MessageType.GroupJoinResponse, 
                        data: new GroupJoinResponse(requestApproved: true, group.Id, groupJoinRequest.ConnectionId),
                        reliable: true, senderClientId: clientId, originTimestamp: 0);
                }
                else
                {
                    _streamingHub.Send(destinationClientId: clientId, messageId: (int)MessageType.GroupJoinResponse, 
                        data: new GroupJoinResponse(requestApproved: false, group.Id, groupJoinRequest.ConnectionId),
                        reliable: true, senderClientId: 0, originTimestamp: 0);
                }
            }
            else
            {
                Debug.Log($"<color=orange>[{nameof(SampleServer)}] The group is not found. GroupId: {groupId}</color>");

                _streamingHub.Send(destinationClientId: clientId, messageId: (int)MessageType.GroupJoinResponse, 
                    data: new GroupJoinResponse(requestApproved: false, groupId, groupJoinRequest.ConnectionId),
                    reliable: true, senderClientId: 0, originTimestamp: 0);
            }
        }

        void OnGroupLeaveRequestReceived(uint clientId, GroupLeaveRequest groupLeaveRequest)
        {
            var groupId = groupLeaveRequest.GroupId;

            Debug.Log($"[{nameof(SampleServer)}] Group leave request received - Client[{clientId}], GroupId: {groupId}");

            if (_streamingHub.TryGetGroup(groupId, out var group))
            {
                if (_streamingHub.TryRemoveClientFromGroup(clientId, groupId))
                {
                    Debug.Log($"<color=lime>[{nameof(SampleServer)}] RemoveClientFromGroup - GroupId: {group.Id}, GroupName: {group.Name}, ClientId: {clientId}</color>");
                    _streamingHub.Broadcast(groupId, (int)MessageType.GroupLeaveResponse,
                        data: new GroupLeaveResponse(requestApproved: true, group.Id, groupLeaveRequest.ConnectionId),
                        reliable: true, senderClientId: clientId, originTimestamp: 0);
                }
            }
        }

        void OnIncomingSignalDequeued(int messageId, uint senderClientId, long originTimestamp, SendOptions sendOptions, ReadOnlySequence<byte> payload)
        {
            UnityEngine.Profiling.Profiler.BeginSample("SampleServer.OnIncomingSignalDequeued");

            _receivedSignalCount++;
            _receivedSignalCountText.text = $"{_receivedSignalCount}";

            if (messageId == 0)
            {
                var message = MessagePackSerializer.Deserialize<string>(payload);
                _latestReceivedMessageText.text = message;
                _senderClientIdText.text = senderClientId.ToString();
            }

            if (sendOptions.StreamingType == StreamingType.All)
            {
                if (!_streamingHub.TryGetGroupId(senderClientId, out var groupId))
                {
                    UnityEngine.Profiling.Profiler.EndSample();
                    return;
                }

                _streamingHub.Broadcast(groupId, messageId, payload, sendOptions.Reliable, senderClientId, originTimestamp);
            }
            UnityEngine.Profiling.Profiler.EndSample();
        }
    }
}
