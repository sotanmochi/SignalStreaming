using System.Runtime.CompilerServices;

namespace SignalStreaming.Serialization
{
    public static partial class SignalFormatterProvider
    {
        static SignalFormatterProvider()
        {
            RegisterQuantizedDataFormatters();
        }

        public static void Register<T>(ISignalFormatter<T> formatter)
        {
            Cache<T>.Formatter = formatter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ISignalFormatter<T> GetFormatter<T>()
        {
            return Cache<T>.Formatter;
        }

        static class Cache<T>
        {
            public static ISignalFormatter<T> Formatter;
        }
    }
}