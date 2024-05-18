namespace SignalStreaming.Transports
{
    public interface IGroup
    {
        string Id { get; }
        string Name { get; }
        bool IsActive { get; }
    }
}