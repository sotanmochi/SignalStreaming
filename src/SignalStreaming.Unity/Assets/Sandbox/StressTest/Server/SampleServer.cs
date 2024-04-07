using System;
using System.Buffers;
using System.Diagnostics;
using MessagePack;
using SignalStreaming.Infrastructure.ENet;
using SignalStreaming.Infrastructure.LiteNetLib;
using SignalStreaming.Quantization;
using SignalStreaming.Serialization;
using UnityEngine;
using Button = UnityEngine.UI.Button;
using Text = UnityEngine.UI.Text;
using Debug = UnityEngine.Debug;

namespace SignalStreaming.Sandbox.StressTest
{
    public class SampleServer : MonoBehaviour
    {
        [SerializeField] ushort _port = 54970;
        [SerializeField] string _connectionKey = "SignalStreaming";
        [SerializeField] string _groupId = "01HP8DMTNKAVNQDWCBMG9NWG8S";

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

        [SerializeField] Text _outgoingSignalCountText;
        [SerializeField] Text _outgoingSignalCountText1;
        [SerializeField] Text _outgoingSignalCountText2;
        [SerializeField] Text _outgoingSignalCountText3;
        [SerializeField] Text _outgoingSignalCountText4;

        readonly Stopwatch _stopwatch = new();

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

        uint _outgoingSignalCount;
        uint _outgoingSignalCount1;
        uint _outgoingSignalCount2;
        uint _outgoingSignalCount3;
        uint _outgoingSignalCount4;

        ISignalSerializer _signalSerializer;
        // BoundedRange[] _worldBounds = new BoundedRange[]
        // {
        //     new BoundedRange(-64f, 64f, 0.001f), // X
        //     new BoundedRange(-16f, 48f, 0.001f), // Y (Height)
        //     new BoundedRange(-64f, 64f, 0.001f), // Z
        // };

        ISignalStreamingHub _streamingHub;
        ISignalTransportHub _transportHub;

        void Awake()
        {
            Application.targetFrameRate = 60;
            _stopwatch.Start();

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

                _outgoingSignalCount = 0;
                _outgoingSignalCount1 = 0;
                _outgoingSignalCount2 = 0;
                _outgoingSignalCount3 = 0;
                _outgoingSignalCount4 = 0;
            });

            _transportHub = new LiteNetLibTransportHub(_port, targetFrameRate: 120, maxGroups: 1);
            _signalSerializer = new SignalSerializer(MessagePackSerializer.DefaultOptions);
            _streamingHub = new SignalStreamingHub(_transportHub, _signalSerializer);

            _streamingHub.OnClientConnectionRequested += OnClientConnectionRequested;
            _streamingHub.OnClientConnected += OnConnected;
            _streamingHub.OnClientDisconnected += OnDisconnected;
            _streamingHub.OnIncomingSignalDequeued += OnIncomingSignalDequeued;
            _streamingHub.OnGroupJoinRequestReceived += OnGroupJoinRequestReceived;
            _streamingHub.OnGroupLeaveRequestReceived += OnGroupLeaveRequestReceived;

            _receivedSignalCountText.text = $"{_receivedSignalCount}";
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

        void Start()
        {
            _transportHub.Start();
            _transportHub.TryAddGroup(_groupId, "DevGroup", out var group);
        }

        void Update()
        {
            _transportHub.DequeueIncomingSignals();
            UpdateView();
        }

        void UpdateView()
        {
            UnityEngine.Profiling.Profiler.BeginSample("SampleServer.UpdateView");

            _receivedSignalCountText.text = $"{_receivedSignalCount}";
            _receivedSignalCountText1.text = $"{_receivedSignalCount1}";
            _receivedSignalCountText2.text = $"{_receivedSignalCount2}";
            _receivedSignalCountText3.text = $"{_receivedSignalCount3}";
            _receivedSignalCountText4.text = $"{_receivedSignalCount4}";

            _outgoingSignalCountText.text = $"{_outgoingSignalCount}";
            _outgoingSignalCountText1.text = $"{_outgoingSignalCount1}";
            _outgoingSignalCountText2.text = $"{_outgoingSignalCount2}";
            _outgoingSignalCountText3.text = $"{_outgoingSignalCount3}";
            _outgoingSignalCountText4.text = $"{_outgoingSignalCount4}";

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

            UnityEngine.Profiling.Profiler.EndSample();
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
                    _streamingHub.Send(signalId: (int)MessageType.GroupJoinResponse, 
                        value: new GroupJoinResponse(requestApproved: false, group.Id, groupJoinRequest.ConnectionId),
                        reliable: true, sourceClientId: 0, destinationClientId: clientId);
                }
            }
            else
            {
                Debug.Log($"<color=orange>[{nameof(SampleServer)}] The group is not found. GroupId: {groupId}</color>");

                _streamingHub.Send(signalId: (int)MessageType.GroupJoinResponse, 
                    value: new GroupJoinResponse(requestApproved: false, groupId, groupJoinRequest.ConnectionId),
                    reliable: true, sourceClientId: 0, destinationClientId: clientId);
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

        void OnIncomingSignalDequeued(int messageId, ReadOnlySequence<byte> payload, SendOptions sendOptions, uint senderClientId)
        {
            UnityEngine.Profiling.Profiler.BeginSample("SampleServer.OnIncomingSignalDequeued");

            _receivedSignalCount++;

            if (messageId == (int)SignalType.PlayerObjectColor)
            {
                _receivedSignalCount1++;

                var quantizedHue = MessagePackSerializer.Deserialize<byte>(payload);
                if (sendOptions.StreamingType == StreamingType.All)
                {
                    if (!_streamingHub.TryGetGroupId(senderClientId, out var groupId))
                    {
                        UnityEngine.Profiling.Profiler.EndSample();
                        return;
                    }

                    _outgoingSignalCount++;
                    _outgoingSignalCount1++;
                    _streamingHub.Broadcast(groupId, messageId, quantizedHue, sendOptions.Reliable, senderClientId, 0);
                }
            }
            else if (messageId == (int)SignalType.PlayerObjectPosition)
            {
                _receivedSignalCount2++;

                // Debug.Log($"[{nameof(SampleServer)}] PlayerObjectPosition - Payload: {payload.Length} [bytes]");

                var position = MessagePackSerializer.Deserialize<Vector3>(payload);

                if (sendOptions.StreamingType == StreamingType.All)
                {
                    if (!_streamingHub.TryGetGroupId(senderClientId, out var groupId))
                    {
                        UnityEngine.Profiling.Profiler.EndSample();
                        return;
                    }

                    _outgoingSignalCount++;
                    _outgoingSignalCount2++;
                    _streamingHub.Broadcast(groupId, messageId, position, sendOptions.Reliable, senderClientId, 0);
                }
            }
            else if (messageId == (int)SignalType.PlayerObjectRotation)
            {
                _receivedSignalCount3++;

                // Debug.Log($"<color=cyan>[{nameof(SampleServer)}] PlayerObjectRotation - Payload: {payload.Length} [bytes]</color>");

                var rotation = MessagePackSerializer.Deserialize<Quaternion>(payload);

                if (sendOptions.StreamingType == StreamingType.All)
                {
                    if (!_streamingHub.TryGetGroupId(senderClientId, out var groupId))
                    {
                        UnityEngine.Profiling.Profiler.EndSample();
                        return;
                    }

                    _outgoingSignalCount++;
                    _outgoingSignalCount3++;
                    _streamingHub.Broadcast(groupId, messageId, rotation, sendOptions.Reliable, senderClientId, 0);
                }
            }
            else if (messageId == (int)SignalType.PlayerObjectQuantizedPosition)
            {
                _receivedSignalCount2++;

                // Debug.Log($"[{nameof(SampleServer)}] PlayerObjectQuantizedPosition - Payload: {payload.Length} [bytes]");

                var quantizedPosition = _signalSerializer.Deserialize<QuantizedVector3>(payload);

                if (sendOptions.StreamingType == StreamingType.All)
                {
                    if (!_streamingHub.TryGetGroupId(senderClientId, out var groupId))
                    {
                        UnityEngine.Profiling.Profiler.EndSample();
                        return;
                    }

                    _outgoingSignalCount++;
                    _outgoingSignalCount2++;
                    _streamingHub.Broadcast(groupId, messageId, quantizedPosition, sendOptions.Reliable, senderClientId, 0);
                }
            }
            else if (messageId == (int)SignalType.PlayerObjectQuantizedRotation)
            {
                _receivedSignalCount3++;

                // Debug.Log($"<color=cyan>[{nameof(SampleServer)}] PlayerObjectQuantizedRotation - Payload: {payload.Length} [bytes]</color>");

                var quantizedRotation = _signalSerializer.Deserialize<QuantizedQuaternion>(payload);

                if (sendOptions.StreamingType == StreamingType.All)
                {
                    if (!_streamingHub.TryGetGroupId(senderClientId, out var groupId))
                    {
                        UnityEngine.Profiling.Profiler.EndSample();
                        return;
                    }

                    _outgoingSignalCount++;
                    _outgoingSignalCount3++;
                    _streamingHub.Broadcast(groupId, messageId, quantizedRotation, sendOptions.Reliable, senderClientId, 0);
                }
            }
            else if (messageId == (int)SignalType.ChangeStressTestState)
            {
                var stressTestState = MessagePackSerializer.Deserialize<StressTestState>(payload);
                if (sendOptions.StreamingType == StreamingType.All)
                {
                    if (!_streamingHub.TryGetGroupId(senderClientId, out var groupId))
                    {
                        UnityEngine.Profiling.Profiler.EndSample();
                        return;
                    }
                    _streamingHub.Broadcast(groupId, messageId, stressTestState, sendOptions.Reliable, senderClientId, 0);
                }
            }
            else if (messageId == (int)SignalType.ChangeColor)
            {
                var colorType = MessagePackSerializer.Deserialize<ColorType>(payload);
                if (sendOptions.StreamingType == StreamingType.All)
                {
                    if (!_streamingHub.TryGetGroupId(senderClientId, out var groupId))
                    {
                        UnityEngine.Profiling.Profiler.EndSample();
                        return;
                    }
                    _streamingHub.Broadcast(groupId, messageId, colorType, sendOptions.Reliable, senderClientId, 0);
                }
            }
            else if (messageId == (int)SignalType.QuantizedHumanPose)
            {
                _receivedSignalCount4++;

                var quantizedHumanPose = _signalSerializer.Deserialize<QuantizedHumanPose>(payload);

                if (sendOptions.StreamingType == StreamingType.All)
                {
                    if (!_streamingHub.TryGetGroupId(senderClientId, out var groupId))
                    {
                        UnityEngine.Profiling.Profiler.EndSample();
                        return;
                    }
                    _streamingHub.Broadcast(groupId, messageId, quantizedHumanPose, sendOptions.Reliable, senderClientId, 0);
                }
            }

            // ------------------------------------------------
            // TODO: Fix a bug or remove this code.
            // if (sendOptions.StreamingType == StreamingType.All)
            // {
            //     if (!_streamingHub.TryGetGroupId(senderClientId, out var groupId))
            //     {
            //         UnityEngine.Profiling.Profiler.EndSample();
            //         return;
            //     }
            //     _streamingHub.Broadcast(groupId, messageId, payload, sendOptions.Reliable, senderClientId, 0);
            // }
            // ------------------------------------------------

            UnityEngine.Profiling.Profiler.EndSample();
        }
    }
}
