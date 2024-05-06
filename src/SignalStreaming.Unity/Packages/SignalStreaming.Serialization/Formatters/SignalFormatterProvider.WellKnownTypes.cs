using SignalStreaming.Serialization.Formatters;

namespace SignalStreaming.Serialization
{
    public static partial class SignalFormatterProvider
    {
        internal static void RegisterWellKnownTypeFormatters()
        {
            Register(new ByteFormatter());
            Register(new StringFormatter());
        }
    }
}