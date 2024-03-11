#pragma warning disable 618
#pragma warning disable 612
#pragma warning disable 414
#pragma warning disable 168
#pragma warning disable CS1591 // document public APIs

#pragma warning disable SA1312 // Variable names should begin with lower-case letter
#pragma warning disable SA1649 // File name should match first type name

namespace SignalStreaming.Serialization.MessagePack.NetStack
{
    public class QuantizationResolver : global::MessagePack.IFormatterResolver
    {
        public static readonly global::MessagePack.IFormatterResolver Instance = new QuantizationResolver();

        private QuantizationResolver()
        {
        }

        public global::MessagePack.Formatters.IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        private static class FormatterCache<T>
        {
            internal static readonly global::MessagePack.Formatters.IMessagePackFormatter<T> Formatter;

            static FormatterCache()
            {
                var f = QuantizationResolverGetFormatterHelper.GetFormatter(typeof(T));
                if (f != null)
                {
                    Formatter = (global::MessagePack.Formatters.IMessagePackFormatter<T>)f;
                }
            }
        }
    }

    internal static class QuantizationResolverGetFormatterHelper
    {
        private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, int> lookup;

        static QuantizationResolverGetFormatterHelper()
        {
            lookup = new global::System.Collections.Generic.Dictionary<global::System.Type, int>(4)
            {
                { typeof(global::NetStack.Quantization.QuantizedVector2), 0 },
                { typeof(global::NetStack.Quantization.QuantizedVector3), 1 },
                { typeof(global::NetStack.Quantization.QuantizedVector4), 2 },
                { typeof(global::NetStack.Quantization.QuantizedQuaternion), 3 },
            };
        }

        internal static object GetFormatter(global::System.Type t)
        {
            int key;
            if (!lookup.TryGetValue(t, out key))
            {
                return null;
            }

            switch (key)
            {
                case 0: return new SignalStreaming.Serialization.MessagePack.NetStack.QuantizedVector2Formatter();
                case 1: return new SignalStreaming.Serialization.MessagePack.NetStack.QuantizedVector3Formatter();
                case 2: return new SignalStreaming.Serialization.MessagePack.NetStack.QuantizedVector4Formatter();
                case 3: return new SignalStreaming.Serialization.MessagePack.NetStack.QuantizedQuaternionFormatter();
                default: return null;
            }
        }
    }
}

#pragma warning restore 168
#pragma warning restore 414
#pragma warning restore 618
#pragma warning restore 612

#pragma warning restore SA1312 // Variable names should begin with lower-case letter
#pragma warning restore SA1649 // File name should match first type name
