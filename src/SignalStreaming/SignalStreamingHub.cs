using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MessagePack;
using DebugLogger = SignalStreaming.DevelopmentOnlyLogger;

namespace SignalStreaming
{
    public sealed class SignalStreamingHub : ISignalStreamingHub
    {
        ISignalTransportHub _transportHub;

        public event ISignalStreamingHub.ConnectionRequestHandler OnClientConnectionRequested;
        public event ISignalStreamingHub.OnDataReceivedEventHandler OnDataReceived;
        public event Action<uint> OnClientConnected;
        public event Action<uint> OnClientDisconnected;
        public event Action<uint, GroupJoinRequest> OnGroupJoinRequestReceived;
        public event Action<uint, GroupLeaveRequest> OnGroupLeaveRequestReceived;

        public SignalStreamingHub(ISignalTransportHub transportHub)
        {
            _transportHub = transportHub;
            _transportHub.OnConnected += OnTransportConnected;
            _transportHub.OnDisconnected += OnTransportDisconnected;
            _transportHub.OnDataReceived += OnDataReceivedInternal;
        }

        public void Dispose()
        {
            _transportHub.OnConnected -= OnTransportConnected;
            _transportHub.OnDisconnected -= OnTransportDisconnected;
            _transportHub.OnDataReceived -= OnDataReceivedInternal;
            _transportHub = null;
        }

        public bool TryGetGroupId(uint clientId, out string groupId)
            => _transportHub.TryGetGroupId(clientId, out groupId);

        public bool TryGetGroup(string groupId, out IGroup group)
            => _transportHub.TryGetGroup(groupId, out group);

        public bool TryAddGroup(string groupId, string groupName, out IGroup group)
            => _transportHub.TryAddGroup(groupId, groupName, out group);

        public bool TryRemoveGroup(string groupId)
            => _transportHub.TryRemoveGroup(groupId);

        public bool TryAddClientToGroup(uint clientId, string groupId)
            => _transportHub.TryAddClientToGroup(clientId, groupId);

        public bool TryRemoveClientFromGroup(uint clientId, string groupId)
            => _transportHub.TryRemoveClientFromGroup(clientId, groupId);

        public void Send<T>(int messageId, uint senderClientId, long originTimestamp, T data, bool reliable, uint destinationClientId)
        {
            var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            var serializedMessage = Serialize(messageId, senderClientId, originTimestamp, transmitTimestamp, data);
            _transportHub.Send(destinationClientId, serializedMessage, reliable);
        }

        public void Send(int messageId, uint senderClientId, long originTimestamp, ReadOnlyMemory<byte> rawMessagePackBlock, bool reliable, uint destinationClientId)
        {
            var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            var serializedMessage = Serialize(messageId, senderClientId, originTimestamp, transmitTimestamp, rawMessagePackBlock);
            _transportHub.Send(destinationClientId, serializedMessage, reliable);
        }

        public void Broadcast<T>(string groupId, int messageId, T data, bool reliable, uint senderClientId, long originTimestamp)
        {
            var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            var serializedMessage = Serialize(messageId, senderClientId, originTimestamp, transmitTimestamp, data);
            _transportHub.Broadcast(groupId, serializedMessage, reliable);
        }

        public void Broadcast(string groupId, int messageId, ReadOnlyMemory<byte> rawMessagePackBlock, bool reliable, uint senderClientId, long originTimestamp)
        {
            var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            var serializedMessage = Serialize(messageId, senderClientId, originTimestamp, transmitTimestamp, rawMessagePackBlock);
            _transportHub.Broadcast(groupId, serializedMessage, reliable);
        }

        public void Broadcast<T>(int messageId, uint senderClientId, long originTimestamp, T data, bool reliable, IReadOnlyList<uint> destinationClientIds)
        {
            var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            var serializedMessage = Serialize(messageId, senderClientId, originTimestamp, transmitTimestamp, data);
            _transportHub.Broadcast(destinationClientIds, serializedMessage, reliable);
        }

        public void Broadcast(int messageId, uint senderClientId, long originTimestamp, ReadOnlyMemory<byte> rawMessagePackBlock, bool reliable, IReadOnlyList<uint> destinationClientIds)
        {
            var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            var serializedMessage = Serialize(messageId, senderClientId, originTimestamp, transmitTimestamp, rawMessagePackBlock);
            _transportHub.Broadcast(destinationClientIds, serializedMessage, reliable);
        }

        void OnTransportConnected(uint clientId)
        {
            var messageId = (int)MessageType.TransportConnected;
            var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            var connectionMessage = SerializeConnectionMessage(
                messageId, originTimestamp: transmitTimestamp, transmitTimestamp, clientId, "ConnectedToHubServer");
            _transportHub.Send(clientId, connectionMessage, reliable: true);
        }

        void OnTransportDisconnected(uint clientId)
        {
            OnClientDisconnected?.Invoke(clientId);
        }

        void OnDataReceivedInternal(uint clientId, ArraySegment<byte> data)
        {
            var reader = new MessagePackReader(data);

            var arrayLength = reader.ReadArrayHeader();
            if (arrayLength != 5)
            {
                throw new InvalidOperationException($"[{nameof(SignalStreamingHub)}] Invalid data format.");
            }

            var messageId = reader.ReadInt32();
            var senderClientId = reader.ReadUInt32();
            var originTimestamp = reader.ReadInt64();
            var sendOptions = MessagePackSerializer.Deserialize<SendOptions>(ref reader);

            var payloadOffset = data.Offset + (int)reader.Consumed;
            var payloadCount = data.Count - (int)reader.Consumed;
            var payload = new ReadOnlyMemory<byte>(data.Array, payloadOffset, payloadCount);

            if (messageId == (int)MessageType.ClientConnectionRequest)
            {
                var connectionRequest = MessagePackSerializer.Deserialize<ClientConnectionRequest>(payload);

                var response = OnClientConnectionRequested != null
                    ? OnClientConnectionRequested.Invoke(senderClientId, connectionRequest)
                    : new ClientConnectionResponse(
                        requestApproved: true,
                        clientId: senderClientId,
                        connectionId: Guid.NewGuid().ToString(),
                        message: "No connection request validation.");

                var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
                var responseMessage = Serialize((int)MessageType.ClientConnectionResponse, 0,
                    originTimestamp, transmitTimestamp, response);

                _transportHub.Send(destinationClientId: senderClientId, responseMessage, reliable: true);

                if (response.RequestApproved)
                {
                    OnClientConnected?.Invoke(senderClientId);
                }
                else
                {
                    _transportHub.Disconnect(senderClientId);
                }
            }
            else if (messageId == (int)MessageType.GroupJoinRequest)
            {
                var joinRequest = MessagePackSerializer.Deserialize<GroupJoinRequest>(payload);
                OnGroupJoinRequestReceived?.Invoke(senderClientId, joinRequest);
            }
            else if (messageId == (int)MessageType.GroupLeaveRequest)
            {
                var leaveRequest = MessagePackSerializer.Deserialize<GroupLeaveRequest>(payload);
                OnGroupLeaveRequestReceived?.Invoke(senderClientId, leaveRequest);
            }
            else
            {
                OnDataReceived?.Invoke(messageId, senderClientId, originTimestamp, sendOptions, payload);
            }
        }

        byte[] Serialize(int messageId, uint senderClientId, long originTimestamp, long transmitTimestamp, ReadOnlyMemory<byte> rawMessagePackBlock)
        {
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            var writer = new MessagePackWriter(bufferWriter);
            writer.WriteArrayHeader(6);
            writer.Write(messageId);
            writer.Write(senderClientId);
            writer.Write(originTimestamp);
            writer.Write(transmitTimestamp);
            // writer.Write(0);
            writer.WriteRaw(rawMessagePackBlock.Span); // NOTE
            writer.Flush();
            return bufferWriter.WrittenSpan.ToArray();
        }

        byte[] Serialize<T>(int messageId, uint senderClientId, long originTimestamp, long transmitTimestamp, T data)
        {
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            var writer = new MessagePackWriter(bufferWriter);
            writer.WriteArrayHeader(6);
            writer.Write(messageId);
            writer.Write(senderClientId);
            writer.Write(originTimestamp);
            writer.Write(transmitTimestamp);
            // writer.Write(0);
            writer.Flush();
            MessagePackSerializer.Serialize(bufferWriter, data);
            return bufferWriter.WrittenSpan.ToArray();
        }

        byte[] SerializeConnectionMessage<T>(int messageId, long originTimestamp, long transmitTimestamp, uint connectingClientId, T data)
        {
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            var writer = new MessagePackWriter(bufferWriter);
            writer.WriteArrayHeader(6);
            writer.Write(messageId);
            writer.Write(0); // NOTE: The sender client ID is set to 0, because this message is sent from the hub server.
            writer.Write(originTimestamp);
            writer.Write(transmitTimestamp);
            writer.Write(connectingClientId);
            writer.Flush();
            MessagePackSerializer.Serialize(bufferWriter, data);
            return bufferWriter.WrittenSpan.ToArray();
        }
    }
}
