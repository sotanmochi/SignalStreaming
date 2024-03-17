using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MessagePack;
using DebugLogger = SignalStreaming.DevelopmentOnlyLogger;

namespace SignalStreaming
{
    public sealed class SignalStreamingHub : ISignalStreamingHub
    {
        ISignalTransportHub _transportHub;
        ISignalSerializer _signalSerializer;

        public event ISignalStreamingHub.ConnectionRequestHandler OnClientConnectionRequested;
        public event ISignalStreamingHub.OnIncomingSignalDequeuedEventHandler OnIncomingSignalDequeued;
        public event Action<uint> OnClientConnected;
        public event Action<uint> OnClientDisconnected;
        public event Action<uint, GroupJoinRequest> OnGroupJoinRequestReceived;
        public event Action<uint, GroupLeaveRequest> OnGroupLeaveRequestReceived;

        public SignalStreamingHub(ISignalTransportHub transportHub, ISignalSerializer signalSerializer)
        {
            _signalSerializer = signalSerializer;
            _transportHub = transportHub;
            _transportHub.OnConnected += OnTransportConnected;
            _transportHub.OnDisconnected += OnTransportDisconnected;
            _transportHub.OnIncomingSignalDequeued += OnIncomingSignalDequeuedInternal;
        }

        public void Dispose()
        {
            _transportHub.OnConnected -= OnTransportConnected;
            _transportHub.OnDisconnected -= OnTransportDisconnected;
            _transportHub.OnIncomingSignalDequeued -= OnIncomingSignalDequeuedInternal;
            _transportHub = null;
            _signalSerializer = null;
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

        public void Send<T>(int signalId, T value, bool reliable, uint sourceClientId, uint destinationClientId)
        {
            var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            var serializedMessage = Serialize(signalId, value, sourceClientId);
            _transportHub.EnqueueOutgoingSignal(destinationClientId, serializedMessage, reliable);
        }

        public void Send(int signalId, ReadOnlyMemory<byte> rawMessagePackBlock, bool reliable, uint sourceClientId, uint destinationClientId)
        {
            throw new NotImplementedException();
            // var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            // var serializedMessage = Serialize(signalId, rawMessagePackBlock, sourceClientId);
            // _transportHub.EnqueueOutgoingSignal(destinationClientId, serializedMessage, reliable);
        }

        public void Broadcast<T>(string groupId, int signalId, T value, bool reliable, uint sourceClientId, long originTimestamp)
        {
            var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            var serializedMessage = Serialize(signalId, value, sourceClientId);
            _transportHub.EnqueueOutgoingSignal(groupId, serializedMessage, reliable);
        }

        public void Broadcast(string groupId, int signalId, ReadOnlyMemory<byte> rawMessagePackBlock, bool reliable, uint sourceClientId, long originTimestamp)
        {
            throw new NotImplementedException();
            // var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            // var serializedMessage = Serialize(signalId, rawMessagePackBlock, sourceClientId);
            // _transportHub.EnqueueOutgoingSignal(groupId, serializedMessage, reliable);
        }

        public void Broadcast<T>(int messageId, uint senderClientId, long originTimestamp, T data, bool reliable, IReadOnlyList<uint> destinationClientIds)
        {
            throw new NotImplementedException();
        }

        public void Broadcast(int messageId, uint senderClientId, long originTimestamp, ReadOnlyMemory<byte> rawMessagePackBlock, bool reliable, IReadOnlyList<uint> destinationClientIds)
        {
            throw new NotImplementedException();
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

        void OnIncomingSignalDequeuedInternal(uint clientId, ReadOnlySequence<byte> data)
        {
            var reader = new MessagePackReader(data);

            var arrayLength = reader.ReadArrayHeader();
            if (arrayLength != 5)
            {
                throw new InvalidOperationException($"[{nameof(SignalStreamingHub)}] Invalid data format.");
            }

            var signalId = reader.ReadInt32();
            var sourceClientId = reader.ReadUInt32();
            // var streamingType = reader.ReadInt64();
            var streamingType = reader.ReadByte();
            var reliable = reader.ReadBoolean();
            var sendOptions = new SendOptions((StreamingType)streamingType, reliable);

            var payloadOffset = (int)reader.Consumed;
            var payloadLength = data.Length - (int)reader.Consumed;
            var payload = data.Slice(payloadOffset, payloadLength);
            
            if (signalId == (int)MessageType.ClientConnectionRequest)
            {
                var connectionRequest = MessagePackSerializer.Deserialize<ClientConnectionRequest>(payload);

                var response = OnClientConnectionRequested != null
                    ? OnClientConnectionRequested.Invoke(sourceClientId, connectionRequest)
                    : new ClientConnectionResponse(
                        requestApproved: true,
                        clientId: sourceClientId,
                        connectionId: Guid.NewGuid().ToString(),
                        message: "No connection request validation.");

                var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
                var responseMessage = Serialize((int)MessageType.ClientConnectionResponse, response, 0);

                _transportHub.Send(destinationClientId: sourceClientId, responseMessage, reliable: true);

                if (response.RequestApproved)
                {
                    OnClientConnected?.Invoke(sourceClientId);
                }
                else
                {
                    _transportHub.Disconnect(sourceClientId);
                }
            }
            else if (signalId == (int)MessageType.GroupJoinRequest)
            {
                var joinRequest = MessagePackSerializer.Deserialize<GroupJoinRequest>(payload);
                OnGroupJoinRequestReceived?.Invoke(sourceClientId, joinRequest);
            }
            else if (signalId == (int)MessageType.GroupLeaveRequest)
            {
                var leaveRequest = MessagePackSerializer.Deserialize<GroupLeaveRequest>(payload);
                OnGroupLeaveRequestReceived?.Invoke(sourceClientId, leaveRequest);
            }
            else
            {
                OnIncomingSignalDequeued?.Invoke(signalId, payload, sendOptions, sourceClientId);
            }
        }

        // TODO: Fix a bug or remove this method.
        // byte[] Serialize(int messageId, uint senderClientId, long originTimestamp, long transmitTimestamp, ReadOnlyMemory<byte> rawMessagePackBlock)
        // {
        //     using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
        //     var writer = new MessagePackWriter(bufferWriter);
        //     writer.WriteArrayHeader(6);
        //     writer.Write(messageId);
        //     writer.Write(senderClientId);
        //     writer.Write(originTimestamp);
        //     writer.Write(transmitTimestamp);
        //     // writer.Write(0);
        //     writer.WriteRaw(rawMessagePackBlock.Span); // NOTE
        //     writer.Flush();
        //     return bufferWriter.WrittenSpan.ToArray();
        // }

        // byte[] Serialize<T>(int messageId, uint senderClientId, long originTimestamp, long transmitTimestamp, T value)
        // {
        //     using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
        //     var writer = new MessagePackWriter(bufferWriter);
        //     writer.WriteArrayHeader(6);
        //     writer.Write(messageId);
        //     writer.Write(senderClientId);
        //     writer.Write(originTimestamp);
        //     writer.Write(transmitTimestamp);
        //     // writer.Write(0);
        //     writer.Flush();
        //     _signalSerializer.Serialize(bufferWriter, value);
        //     return bufferWriter.WrittenSpan.ToArray();
        // }

        byte[] Serialize<T>(int signalId, T value, uint sourceClientId)
        {
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            var writer = new MessagePackWriter(bufferWriter);
            writer.WriteArrayHeader(3);
            writer.Write(signalId);
            writer.Write(sourceClientId);
            writer.Flush();
            _signalSerializer.Serialize(bufferWriter, value);
            return bufferWriter.WrittenSpan.ToArray();
        }

        byte[] SerializeConnectionMessage<T>(int messageId, long originTimestamp, long transmitTimestamp, uint connectingClientId, T value)
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
            _signalSerializer.Serialize(bufferWriter, value);
            return bufferWriter.WrittenSpan.ToArray();
        }
    }
}
