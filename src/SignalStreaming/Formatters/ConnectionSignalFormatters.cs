using System;
using SignalStreaming.Serialization;

namespace SignalStreaming
{
    public sealed class ClientConnectionRequestFormatter : ISignalFormatter<ClientConnectionRequest>
    {
        public void Serialize(BitBuffer bitBuffer, in ClientConnectionRequest value)
        {
            var connectionKeyLength = value.ConnectionKey.Length;
            bitBuffer.AddInt(connectionKeyLength);
            for (var i = 0; i < connectionKeyLength; i++)
            {
                bitBuffer.AddByte(value.ConnectionKey[i]);
            }
        }

        public ClientConnectionRequest Deserialize(BitBuffer bitBuffer)
        {
            var connectionKeyLength = bitBuffer.ReadInt();
            var connectionKey = new byte[connectionKeyLength];
            for (var i = 0; i < connectionKeyLength; i++)
            {
                connectionKey[i] = bitBuffer.ReadByte();
            }
            return new ClientConnectionRequest(connectionKey);
        }

        public void DeserializeTo(ref ClientConnectionRequest output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class ClientConnectionResponseFormatter : ISignalFormatter<ClientConnectionResponse>
    {
        public void Serialize(BitBuffer bitBuffer, in ClientConnectionResponse value)
        {
            bitBuffer.AddBool(value.RequestApproved);
            bitBuffer.AddUInt(value.ClientId);
            bitBuffer.AddString(value.ConnectionId);
            bitBuffer.AddString(value.Message);
        }

        public ClientConnectionResponse Deserialize(BitBuffer bitBuffer)
        {
            var requestApproved = bitBuffer.ReadBool();
            var clientId = bitBuffer.ReadUInt();
            var connectionId = bitBuffer.ReadString();
            var message = bitBuffer.ReadString();
            return new ClientConnectionResponse(requestApproved, clientId, connectionId, message);
        }

        public void DeserializeTo(ref ClientConnectionResponse output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }
}