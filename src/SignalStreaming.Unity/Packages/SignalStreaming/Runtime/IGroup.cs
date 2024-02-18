using System;

namespace SignalStreaming
{
    public interface IGroup
    {
        string Id { get; }
        string Name { get; }
        bool IsActive { get; }
    }
}
