using System;
using MessagePack;
using SignalStreaming.Infrastructure.ENet;
using UnityEngine;

namespace SignalStreaming.Samples.ENetSample
{
    public class SampleServer : MonoBehaviour
    {
        [SerializeField] ushort _port = 3333;
        [SerializeField] string _connectionKey = "SignalStreaming";
        [SerializeField] string _groupId = "01HP8DMTNKAVNQDWCBMG9NWG8S";

        ISignalStreamingHub _streamingHub;
        ISignalTransportHub _transportHub;

        void Awake()
        {
            // _transportHub = new ENetTransportHub(_port, useAnotherThread: true, targetFrameRate: 60, isBackground: true);
            _transportHub = new ENetTransportHub(_port, useAnotherThread: false, targetFrameRate: 60, isBackground: true);
            _streamingHub = new SignalStreamingHub(_transportHub);

            _streamingHub.OnClientConnectionRequested += OnClientConnectionRequested;
            _streamingHub.OnClientConnected += OnConnected;
            _streamingHub.OnClientDisconnected += OnDisconnected;
            _streamingHub.OnDataReceived += OnDataReceived;
            _streamingHub.OnGroupJoinRequestReceived += OnGroupJoinRequestReceived;
            _streamingHub.OnGroupLeaveRequestReceived += OnGroupLeaveRequestReceived;
        }

        void Start()
        {
            _transportHub.Start();
            _transportHub.TryAddGroup(_groupId, "DevGroup", out var group);
        }

        void Update()
        {
            _transportHub.PollEvent();
        }

        void OnDestroy()
        {
            _streamingHub.OnClientConnectionRequested -= OnClientConnectionRequested;
            _streamingHub.OnClientConnected -= OnConnected;
            _streamingHub.OnClientDisconnected -= OnDisconnected;
            _streamingHub.OnDataReceived -= OnDataReceived;
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

        void OnDataReceived(int messageId, uint senderClientId, long originTimestamp, SendOptions sendOptions, ReadOnlyMemory<byte> payload)
        {
            // Debug.Log($"[{nameof(SampleServer)}] Data received from Client[{senderClientId}]. " +
            //     $"Message ID: {messageId}, Payload.Length: {payload.Length}");

            if (messageId == 0)
            {
                var message = MessagePackSerializer.Deserialize<string>(payload);
                // Debug.Log($"<color=lime>[{nameof(SampleServer)}] Received message: {message}</color>");
            }

            if (sendOptions.StreamingType == StreamingType.All)
            {
                if (!_streamingHub.TryGetGroupId(senderClientId, out var groupId))
                {
                    return;
                }

                _streamingHub.Broadcast(groupId, messageId, payload, sendOptions.Reliable, senderClientId, originTimestamp);
            }
        }
    }
}
