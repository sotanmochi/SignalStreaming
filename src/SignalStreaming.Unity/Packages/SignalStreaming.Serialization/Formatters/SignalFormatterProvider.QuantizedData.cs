using SignalStreaming.Serialization.Formatters;

namespace SignalStreaming.Serialization
{
    public static partial class SignalFormatterProvider
    {
        internal static void RegisterQuantizedDataFormatters()
        {
            Register(new QuantizedVectorFormatter());
            Register(new QuantizedVector3Formatter());
            Register(new QuantizedQuaternionFormatter());
            Register(new QuantizedHumanoidPoseFormatter());
        }
    }
}