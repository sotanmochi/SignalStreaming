using System;
using ENet;

namespace SignalStreaming.Infrastructure.ENet
{
    public sealed class ENetGroup : IGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public Peer[] Clients { get; internal set; }
    }
}
