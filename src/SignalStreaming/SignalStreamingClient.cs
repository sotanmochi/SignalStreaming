using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using DebugLogger = SignalStreaming.DevelopmentOnlyLogger;

namespace SignalStreaming
{
    public sealed class SignalStreamingClient : ISignalStreamingClient
    {
        static readonly string DefaultDisconnectionReason = "Disconnected from server";

        ISignalSerializer _signalSerializer;
        ISignalTransport _transport;
        string _connectionId = "";

        uint _clientId;
        bool _connecting;
        bool _connected;
        string _disconnectionReason = DefaultDisconnectionReason;
        byte[] _connectionRequestData = new byte[0];
        TaskCompletionSource<bool> _connectionTcs;

        bool _joining;
        TaskCompletionSource<bool> _joinTcs;

        public event ISignalStreamingClient.OnIncomingSignalDequeuedEventHandler OnIncomingSignalDequeued;
        public event Action<uint> OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<GroupJoinResponse> OnGroupJoinResponseReceived;
        public event Action<GroupLeaveResponse> OnGroupLeaveResponseReceived;

        public string ConnectionId => _connectionId;
        public uint ClientId => _clientId;
        public bool IsConnecting => _connecting;
        public bool IsConnected => _connected;

        public SignalStreamingClient(ISignalTransport transport, ISignalSerializer signalSerializer)
        {
            _signalSerializer = signalSerializer;
            _transport = transport;
            _transport.OnDisconnected += OnTransportDisconnected;
            _transport.OnIncomingSignalDequeued += OnTransportIncomingSignalDequeued;
        }

        public void Dispose()
        {
            DisconnectAsync();
            _transport.OnDisconnected -= OnTransportDisconnected;
            _transport.OnIncomingSignalDequeued -= OnTransportIncomingSignalDequeued;
            _transport = null;
            _signalSerializer = null;
        }

        public async Task<bool> ConnectAsync<T>(T connectParameters, CancellationToken cancellationToken = default) where T : IConnectParameters
        {
            if (_connected || _connecting) return await _connectionTcs.Task;

            _connectionTcs = new TaskCompletionSource<bool>();
            _connecting = true;

            try
            {
                _connectionRequestData = connectParameters.ConnectionRequestData;
                await _transport.ConnectAsync(connectParameters, cancellationToken);
            }
            catch (Exception e)
            {
                _connected = false;
                _connectionTcs.SetResult(false);
                throw;
            }
            finally
            {
                _connecting = false;
            }

            return await _connectionTcs.Task;
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            await _transport.DisconnectAsync(cancellationToken);
            _connected = false;
        }

        public async Task<bool> JoinGroupAsync(string groupId, CancellationToken cancellationToken = default)
        {
            if (_joining) return await _joinTcs.Task;

            _joinTcs = new TaskCompletionSource<bool>();
            _joining = true;

            try
            {
                var request = new GroupJoinRequest(groupId, _connectionId);
                var sendOptions = new SendOptions(StreamingType.ToHubServer, reliable: true);
                var originTimestamp = TimestampProvider.GetCurrentTimestamp();

                var payload = Serialize(messageId: (int)MessageType.GroupJoinRequest, value: request,
                    sendOptions: sendOptions, senderClientId: _clientId, originTimestamp: originTimestamp);

                _transport.EnqueueOutgoingSignal(payload, sendOptions);
            }
            catch (Exception e)
            {
                _joining = false;
                _joinTcs.SetResult(false);
                throw;
            }

            return await _joinTcs.Task;
        }

        public async Task LeaveGroupAsync(string groupId, CancellationToken cancellationToken = default)
        {
            var request = new GroupLeaveRequest(groupId, _connectionId);
            var sendOptions = new SendOptions(StreamingType.ToHubServer, reliable: true);
            var originTimestamp = TimestampProvider.GetCurrentTimestamp();

            var payload = Serialize(messageId: (int)MessageType.GroupLeaveRequest, value: request,
                sendOptions: sendOptions, senderClientId: _clientId, originTimestamp: originTimestamp);
            _transport.EnqueueOutgoingSignal(payload, sendOptions);
        }

        public void Send(int messageId, ReadOnlySequence<byte> rawMessageBuffer, SendOptions sendOptions, uint[] destinationClientIds = null)
        {
            if (!_connected) return;
            var originTimestamp = TimestampProvider.GetCurrentTimestamp();
            var payload = Serialize(messageId, senderClientId: _clientId, originTimestamp, sendOptions, rawMessageBuffer);            
            _transport.EnqueueOutgoingSignal(payload, sendOptions);
        }

        public void Send<T>(int messageId, T data, SendOptions sendOptions, uint[] destinationClientIds = null)
        {
            if (!_connected) return;
            var originTimestamp = TimestampProvider.GetCurrentTimestamp();
            var payload = Serialize(messageId, senderClientId: _clientId, originTimestamp, sendOptions, data);
            _transport.EnqueueOutgoingSignal(payload, sendOptions);
        }

        void OnTransportDisconnected()
        {
            if (_clientId <= 0 && !_connected) return;

            DebugLogger.Log($"<color=orange>[{nameof(SignalStreamingClient)}] OnDisconnected</color>");

            _connected = false;
            OnDisconnected?.Invoke(_disconnectionReason);

            // Reset
            _disconnectionReason = DefaultDisconnectionReason;
            _clientId = 0;
        }

        void OnTransportIncomingSignalDequeued(ReadOnlySequence<byte> signalBytes)
        {
            var reader = new MessagePackReader(signalBytes);

            var arrayLength = reader.ReadArrayHeader();
            if (arrayLength != 6)
            {
                throw new InvalidOperationException($"[{nameof(SignalStreamingClient)}] Invalid data format.");
            }

            var messageId = reader.ReadInt32();
            var senderClientId = reader.ReadUInt32();
            var originTimestamp = reader.ReadInt64();
            var transmitTimestamp = reader.ReadInt64();

            if (messageId == (int)MessageType.TransportConnected)
            {
                var connectedClientId = reader.ReadUInt32();
                HandleTransportConnectionResponse(connectedClientId);
            }
            else if (messageId == (int)MessageType.ClientConnectionResponse)
            {
                var payloadOffset = (int)reader.Consumed;
                var payloadLength = signalBytes.Length - (int)reader.Consumed;
                var payload = signalBytes.Slice(payloadOffset, payloadLength);
                HandleConnectionResponse(payload);
            }
            else if (messageId == (int)MessageType.GroupJoinResponse)
            {
                var payloadOffset = (int)reader.Consumed;
                var payloadLength = signalBytes.Length - (int)reader.Consumed;
                var payload = signalBytes.Slice(payloadOffset, payloadLength);
                HandleGroupJoinResponse(payload);
            }
            else if (messageId == (int)MessageType.GroupLeaveResponse)
            {
                var payloadOffset = (int)reader.Consumed;
                var payloadLength = signalBytes.Length - (int)reader.Consumed;
                var payload = signalBytes.Slice(payloadOffset, payloadLength);
                HandleGroupLeaveResponse(payload);
            }
            else
            {
                var payloadOffset = (int)reader.Consumed;
                var payloadLength = signalBytes.Length - (int)reader.Consumed;
                var payload = signalBytes.Slice(payloadOffset, payloadLength);
                OnIncomingSignalDequeued?.Invoke(messageId, senderClientId, originTimestamp, transmitTimestamp, payload);
            }
        }

        void HandleTransportConnectionResponse(uint clientId)
        {
            SendConnectionRequest(clientId);
        }

        void SendConnectionRequest(uint clientId)
        {
            var sendOptions = new SendOptions(
                streamingType: StreamingType.ToHubServer,
                reliable: true
            );

            var originTimestamp = TimestampProvider.GetCurrentTimestamp();

            var connectionRequest = new ClientConnectionRequest(_connectionRequestData);
            var data = Serialize((int)MessageType.ClientConnectionRequest, clientId, originTimestamp, sendOptions, connectionRequest);
            _transport.EnqueueOutgoingSignal(data, sendOptions);
        }

        void HandleConnectionResponse(ReadOnlySequence<byte> data)
        {
            var response = MessagePackSerializer.Deserialize<ClientConnectionResponse>(data);
            DebugLogger.Log($"<color=cyan>[{nameof(SignalStreamingClient)}] Connection result: {response.Message}</color>");

            if (response.RequestApproved)
            {
                _connectionId = response.ConnectionId;
                _clientId =  response.ClientId;
                _connected = true;
                _connectionTcs.SetResult(true);
                OnConnected?.Invoke(_clientId);
            }
            else
            {
                _disconnectionReason = response.Message;
                _connectionTcs.SetResult(false);
            }
        }

        void HandleGroupJoinResponse(ReadOnlySequence<byte> data)
        {
            var response = MessagePackSerializer.Deserialize<GroupJoinResponse>(data);

            if (_joining)
            {
                _joining = false;
                _joinTcs.SetResult(response.RequestApproved);
            }

            if (response.RequestApproved)
            {
                OnGroupJoinResponseReceived?.Invoke(response);
            }
        }

        void HandleGroupLeaveResponse(ReadOnlySequence<byte> data)
        {
            var response = MessagePackSerializer.Deserialize<GroupLeaveResponse>(data);
            if (response.RequestApproved)
            {
                OnGroupLeaveResponseReceived?.Invoke(response);
            }
        }

        // TODO: Fix a bug or remove this method
        // byte[] Serialize(int messageId, uint senderClientId, long originTimestamp, SendOptions sendOptions, ReadOnlySequence<byte> rawMessageBuffer)
        // {
        //     using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
        //     var writer = new MessagePackWriter(bufferWriter);
        //     writer.WriteArrayHeader(6);
        //     writer.Write(messageId);
        //     writer.Write(senderClientId);
        //     writer.Write(originTimestamp);
        //     writer.Write((byte)sendOptions.StreamingType);
        //     writer.Write(sendOptions.Reliable);
        //     writer.Flush();
        //     writer.WriteRaw(rawMessageBuffer); // NOTE
        //     writer.Flush();
        //     return bufferWriter.WrittenSpan.ToArray();
        // }

        byte[] Serialize<T>(int messageId, uint senderClientId, long originTimestamp, SendOptions sendOptions, T value)
        {
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            var writer = new MessagePackWriter(bufferWriter);
            writer.WriteArrayHeader(6);
            writer.Write(messageId);
            writer.Write(senderClientId);
            writer.Write(originTimestamp);
            writer.Write((byte)sendOptions.StreamingType);
            writer.Write(sendOptions.Reliable);
            writer.Flush();
            _signalSerializer.Serialize(bufferWriter, value);
            return bufferWriter.WrittenSpan.ToArray();
        }
    }
}
