using System;

namespace SignalStreaming
{
    public static class TimestampProvider
    {
        public static long GetCurrentTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
