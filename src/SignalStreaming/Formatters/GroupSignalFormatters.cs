using System;
using SignalStreaming.Serialization;

namespace SignalStreaming
{
    public sealed class GroupJoinRequestFomatter : ISignalFormatter<GroupJoinRequest>
    {
        public void Serialize(BitBuffer bitBuffer, in GroupJoinRequest value)
        {
            bitBuffer.AddString(value.GroupId);
            bitBuffer.AddString(value.ConnectionId);
        }

        public GroupJoinRequest Deserialize(BitBuffer bitBuffer)
        {
            var groupId = bitBuffer.ReadString();
            var connectionId = bitBuffer.ReadString();
            return new GroupJoinRequest(groupId, connectionId);
        }

        public void DeserializeTo(ref GroupJoinRequest output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class GroupJoinResponseFormatter : ISignalFormatter<GroupJoinResponse>
    {
        public void Serialize(BitBuffer bitBuffer, in GroupJoinResponse value)
        {
            bitBuffer.AddBool(value.RequestApproved);
            bitBuffer.AddString(value.GroupId);
            bitBuffer.AddString(value.ConnectionId);
        }

        public GroupJoinResponse Deserialize(BitBuffer bitBuffer)
        {
            var requestApproved = bitBuffer.ReadBool();
            var groupId = bitBuffer.ReadString();
            var connectionId = bitBuffer.ReadString();
            return new GroupJoinResponse(requestApproved, groupId, connectionId);
        }

        public void DeserializeTo(ref GroupJoinResponse output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class GroupLeaveRequestFormatter : ISignalFormatter<GroupLeaveRequest>
    {
        public void Serialize(BitBuffer bitBuffer, in GroupLeaveRequest value)
        {
            bitBuffer.AddString(value.GroupId);
            bitBuffer.AddString(value.ConnectionId);
        }

        public GroupLeaveRequest Deserialize(BitBuffer bitBuffer)
        {
            var groupId = bitBuffer.ReadString();
            var connectionId = bitBuffer.ReadString();
            return new GroupLeaveRequest(groupId, connectionId);
        }

        public void DeserializeTo(ref GroupLeaveRequest output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class GroupLeaveResponseFormatter : ISignalFormatter<GroupLeaveResponse>
    {
        public void Serialize(BitBuffer bitBuffer, in GroupLeaveResponse value)
        {
            bitBuffer.AddBool(value.RequestApproved);
            bitBuffer.AddString(value.GroupId);
            bitBuffer.AddString(value.ConnectionId);
        }

        public GroupLeaveResponse Deserialize(BitBuffer bitBuffer)
        {
            var requestApproved = bitBuffer.ReadBool();
            var groupId = bitBuffer.ReadString();
            var connectionId = bitBuffer.ReadString();
            return new GroupLeaveResponse(requestApproved, groupId, connectionId);
        }

        public void DeserializeTo(ref GroupLeaveResponse output, BitBuffer bitBuffer)
        {
            throw new NotImplementedException();
        }
    }
}