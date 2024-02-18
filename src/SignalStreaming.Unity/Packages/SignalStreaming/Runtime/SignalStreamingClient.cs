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

        public event ISignalStreamingClient.OnDataReceivedEventHandler OnDataReceived;
        public event Action<uint> OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<GroupJoinResponse> OnGroupJoinResponseReceived;
        public event Action<GroupLeaveResponse> OnGroupLeaveResponseReceived;

        public string ConnectionId => _connectionId;
        public uint ClientId => _clientId;
        public bool IsConnecting => _connecting;
        public bool IsConnected => _connected;

        public SignalStreamingClient(ISignalTransport transport)
        {
            _transport = transport;
            _transport.OnDisconnected += OnTransportDisconnected;
            _transport.OnDataReceived += OnTransportDataReceived;
        }

        public void Dispose()
        {
            DisconnectAsync();
            _transport.OnDisconnected -= OnTransportDisconnected;
            _transport.OnDataReceived -= OnTransportDataReceived;
            _transport = null;
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

                var payload = Serialize(messageId: (int)MessageType.GroupJoinRequest, message: request,
                    sendOptions: sendOptions, senderClientId: _clientId, originTimestamp: originTimestamp);

                _transport.Send(payload, sendOptions, null);
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

            var payload = Serialize(messageId: (int)MessageType.GroupLeaveRequest, message: request,
                sendOptions: sendOptions, senderClientId: _clientId, originTimestamp: originTimestamp);

            _transport.Send(payload, sendOptions, null);
        }

        public void Send(int messageId, ReadOnlySequence<byte> rawMessageBuffer, SendOptions sendOptions, uint[] destinationClientIds = null)
        {
            if (!_connected) return;
            var originTimestamp = TimestampProvider.GetCurrentTimestamp();
            var payload = Serialize(messageId, senderClientId: _clientId, originTimestamp, sendOptions, rawMessageBuffer);
            _transport.Send(payload, sendOptions, destinationClientIds);
        }

        public void Send<T>(int messageId, T data, SendOptions sendOptions, uint[] destinationClientIds = null)
        {
            if (!_connected) return;
            var originTimestamp = TimestampProvider.GetCurrentTimestamp();
            var payload = Serialize(messageId, senderClientId: _clientId, originTimestamp, sendOptions, data);
            _transport.Send(payload, sendOptions, destinationClientIds);
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

        void OnTransportDataReceived(ArraySegment<byte> data)
        {
            var reader = new MessagePackReader(data);

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
                var payloadOffset = data.Offset + (int)reader.Consumed;
                var payloadCount = data.Count - (int)reader.Consumed;
                var payload = new ReadOnlyMemory<byte>(data.Array, payloadOffset, payloadCount);
                HandleConnectionResponse(payload);
            }
            else if (messageId == (int)MessageType.GroupJoinResponse)
            {
                var payloadOffset = data.Offset + (int)reader.Consumed;
                var payloadCount = data.Count - (int)reader.Consumed;
                var payload = new ReadOnlyMemory<byte>(data.Array, payloadOffset, payloadCount);
                HandleGroupJoinResponse(payload);
            }
            else if (messageId == (int)MessageType.GroupLeaveResponse)
            {
                var payloadOffset = data.Offset + (int)reader.Consumed;
                var payloadCount = data.Count - (int)reader.Consumed;
                var payload = new ReadOnlyMemory<byte>(data.Array, payloadOffset, payloadCount);
                HandleGroupLeaveResponse(payload);
            }
            else
            {
                var payloadOffset = data.Offset + (int)reader.Consumed;
                var payloadCount = data.Count - (int)reader.Consumed;
                var payload = new ReadOnlyMemory<byte>(data.Array, payloadOffset, payloadCount);
                OnDataReceived?.Invoke(messageId, senderClientId, originTimestamp, transmitTimestamp, payload);
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
            _transport.Send(data, sendOptions);
        }

        void HandleConnectionResponse(ReadOnlyMemory<byte> data)
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

        void HandleGroupJoinResponse(ReadOnlyMemory<byte> data)
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

        void HandleGroupLeaveResponse(ReadOnlyMemory<byte> data)
        {
            var response = MessagePackSerializer.Deserialize<GroupLeaveResponse>(data);
            if (response.RequestApproved)
            {
                OnGroupLeaveResponseReceived?.Invoke(response);
            }
        }

        byte[] Serialize(int messageId, uint senderClientId, long originTimestamp, SendOptions sendOptions, ReadOnlySequence<byte> rawMessageBuffer)
        {
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            var writer = new MessagePackWriter(bufferWriter);
            writer.WriteArrayHeader(5);
            writer.Write(messageId);
            writer.Write(senderClientId);
            writer.Write(originTimestamp);
            writer.Flush();
            MessagePackSerializer.Serialize(bufferWriter, sendOptions);
            writer.WriteRaw(rawMessageBuffer); // NOTE
            writer.Flush();
            return bufferWriter.WrittenSpan.ToArray();
        }

        byte[] Serialize<T>(int messageId, uint senderClientId, long originTimestamp, SendOptions sendOptions, T message)
        {
            using var bufferWriter = ArrayPoolBufferWriter.RentThreadStaticWriter();
            var writer = new MessagePackWriter(bufferWriter);
            writer.WriteArrayHeader(5);
            writer.Write(messageId);
            writer.Write(senderClientId);
            writer.Write(originTimestamp);
            writer.Flush();
            MessagePackSerializer.Serialize(bufferWriter, sendOptions);
            MessagePackSerializer.Serialize(bufferWriter, message);
            return bufferWriter.WrittenSpan.ToArray();
        }
    }
}
