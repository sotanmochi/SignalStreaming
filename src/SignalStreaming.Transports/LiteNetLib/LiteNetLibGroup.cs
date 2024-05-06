using System;
using LiteNetLib;

namespace SignalStreaming.Infrastructure.LiteNetLib
{
    public sealed class LiteNetLibGroup : IGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public NetPeer[] Clients { get; internal set; }
    }
}
