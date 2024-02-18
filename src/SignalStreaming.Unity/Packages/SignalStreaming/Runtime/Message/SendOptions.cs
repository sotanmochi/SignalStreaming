using MessagePack;

namespace SignalStreaming
{
    [MessagePackObject]
    public struct SendOptions
    {
        [Key(0)]
        public readonly StreamingType StreamingType;
        [Key(1)]
        public readonly bool Reliable;
        
        public SendOptions(StreamingType streamingType, bool reliable)
        {
            StreamingType = streamingType;
            Reliable = reliable;
        }
    }

    public enum StreamingType : byte
    {
        ToHubServer = 0,
        ToOneClient = 1,
        ToManyClients = 2,
        ExceptSelf = 3,
        ExceptOne = 4,
        ExceptMany = 5,
        All = 6,
    }
}
