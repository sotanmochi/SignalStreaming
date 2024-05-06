using System;
using System.Buffers;
using MessagePack;
using SignalStreaming.Serialization;
using DebugLogger = SignalStreaming.DevelopmentOnlyLogger;

namespace SignalStreaming
{
    public delegate ClientConnectionResponse ConnectionRequestHandler(uint clientId, ClientConnectionRequest connectionRequest);
    public delegate void OnIncomingSignalDequeuedEventHandler(int signalId, ReadOnlySequence<byte> bytes, SendOptions sendOptions, uint sourceClientId);

    public sealed class SignalStreamingHub : IDisposable
    {
        ISignalTransportHub _transportHub;

        public event ConnectionRequestHandler OnClientConnectionRequested;
        public event OnIncomingSignalDequeuedEventHandler OnIncomingSignalDequeued;
        public event Action<uint> OnClientConnected;
        public event Action<uint> OnClientDisconnected;
        public event Action<uint, GroupJoinRequest> OnGroupJoinRequestReceived;
        public event Action<uint, GroupLeaveRequest> OnGroupLeaveRequestReceived;

        public SignalStreamingHub(ISignalTransportHub transportHub)
        {
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
            var serializedSignal = SerializeSignal(signalId, value, sourceClientId);
            _transportHub.EnqueueOutgoingSignal(destinationClientId, serializedSignal, reliable);
        }

        public void SendRawBytes(int signalId, ReadOnlySequence<byte> bytes, bool reliable, uint sourceClientId, uint destinationClientId)
        {
            var serializedSignal = SerializeRawBytesSignal(signalId, bytes, sourceClientId);
            _transportHub.EnqueueOutgoingSignal(destinationClientId, serializedSignal, reliable);
        }

        public void Broadcast<T>(string groupId, int signalId, T value, bool reliable, uint sourceClientId)
        {
            var serializedSignal = SerializeSignal(signalId, value, sourceClientId);
            _transportHub.EnqueueOutgoingSignal(groupId, serializedSignal, reliable);
        }

        public void BroadcastRawBytes(string groupId, int signalId, ReadOnlySequence<byte> bytes, bool reliable, uint sourceClientId)
        {
            var serializedSignal = SerializeRawBytesSignal(signalId, bytes, sourceClientId);
            _transportHub.EnqueueOutgoingSignal(groupId, serializedSignal, reliable);
        }

        void OnTransportConnected(uint clientId)
        {
            var messageId = (int)MessageType.TransportConnected;
            var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
            var connectionMessage = SerializeConnectionMessage(
                messageId, originTimestamp: transmitTimestamp, transmitTimestamp, clientId, "ConnectedToHubServer");
            _transportHub.EnqueueOutgoingSignal(clientId, connectionMessage, reliable: true);
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
                var connectionRequest = SignalSerializer.Deserialize<ClientConnectionRequest>(payload);

                var response = OnClientConnectionRequested != null
                    ? OnClientConnectionRequested.Invoke(sourceClientId, connectionRequest)
                    : new ClientConnectionResponse(
                        requestApproved: true,
                        clientId: sourceClientId,
                        connectionId: Guid.NewGuid().ToString(),
                        message: "No connection request validation.");

                var transmitTimestamp = TimestampProvider.GetCurrentTimestamp();
                var responseMessage = SerializeSignal((int)MessageType.ClientConnectionResponse, response, 0);

                _transportHub.EnqueueOutgoingSignal(sourceClientId, responseMessage, reliable: true);

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
                var joinRequest = SignalSerializer.Deserialize<GroupJoinRequest>(payload);
                OnGroupJoinRequestReceived?.Invoke(sourceClientId, joinRequest);
            }
            else if (signalId == (int)MessageType.GroupLeaveRequest)
            {
                var leaveRequest = SignalSerializer.Deserialize<GroupLeaveRequest>(payload);
                OnGroupLeaveRequestReceived?.Invoke(sourceClientId, leaveRequest);
            }
            else
            {
                OnIncomingSignalDequeued?.Invoke(signalId, payload, sendOptions, sourceClientId);
            }
        }

        ReadOnlySpan<byte> SerializeRawBytesSignal(int signalId, ReadOnlySequence<byte> bytes, uint sourceClientId)
        {
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            var writer = new MessagePackWriter(bufferWriter);
            writer.WriteArrayHeader(3);
            writer.Write(signalId);
            writer.Write(sourceClientId);
            writer.WriteRaw(bytes);
            writer.Flush();
            return bufferWriter.WrittenSpan;
        }

        ReadOnlySpan<byte> SerializeSignal<T>(int signalId, T value, uint sourceClientId)
        {
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            var writer = new MessagePackWriter(bufferWriter);
            writer.WriteArrayHeader(3);
            writer.Write(signalId);
            writer.Write(sourceClientId);
            writer.Flush();
            SignalSerializer.Serialize(bufferWriter, value);
            return bufferWriter.WrittenSpan;
        }

        ReadOnlySpan<byte> SerializeConnectionMessage<T>(int signalId, long originTimestamp, long transmitTimestamp, uint connectingClientId, T value)
        {
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            var writer = new MessagePackWriter(bufferWriter);
            writer.WriteArrayHeader(6);
            writer.Write(signalId);
            writer.Write(0); // NOTE: The sender client ID is set to 0, because this message is sent from the hub server.
            writer.Write(originTimestamp);
            writer.Write(transmitTimestamp);
            writer.Write(connectingClientId);
            writer.Flush();
            SignalSerializer.Serialize(bufferWriter, value);
            return bufferWriter.WrittenSpan;
        }
    }
}
