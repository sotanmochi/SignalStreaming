#pragma warning disable 618
#pragma warning disable 612
#pragma warning disable 414
#pragma warning disable 168
#pragma warning disable CS1591 // document public APIs

#pragma warning disable SA1129 // Do not use default value type constructor
#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1312 // Variable names should begin with lower-case letter
#pragma warning disable SA1403 // File may only contain a single namespace
#pragma warning disable SA1649 // File name should match first type name

namespace SignalStreaming.Serialization.MessagePack.NetStack
{
    public sealed class QuantizedVector2Formatter : global::MessagePack.Formatters.IMessagePackFormatter<global::SignalStreaming.Quantization.QuantizedVector2>
    {
        public void Serialize(ref global::MessagePack.MessagePackWriter writer, global::SignalStreaming.Quantization.QuantizedVector2 value, global::MessagePack.MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            writer.Write(value.x);
            writer.Write(value.y);
        }

        public global::SignalStreaming.Quantization.QuantizedVector2 Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                throw new global::System.InvalidOperationException("typecode is null, struct not supported");
            }

            options.Security.DepthStep(ref reader);
            var length = reader.ReadArrayHeader();
            var __x__ = default(uint);
            var __y__ = default(uint);

            for (int i = 0; i < length; i++)
            {
                switch (i)
                {
                    case 0:
                        __x__ = reader.ReadUInt32();
                        break;
                    case 1:
                        __y__ = reader.ReadUInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new global::SignalStreaming.Quantization.QuantizedVector2(__x__, __y__);
            reader.Depth--;
            return ____result;
        }
    }

    public sealed class QuantizedVector3Formatter : global::MessagePack.Formatters.IMessagePackFormatter<global::SignalStreaming.Quantization.QuantizedVector3>
    {
        public void Serialize(ref global::MessagePack.MessagePackWriter writer, global::SignalStreaming.Quantization.QuantizedVector3 value, global::MessagePack.MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(3);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        public global::SignalStreaming.Quantization.QuantizedVector3 Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                throw new global::System.InvalidOperationException("typecode is null, struct not supported");
            }

            options.Security.DepthStep(ref reader);
            var length = reader.ReadArrayHeader();
            var __x__ = default(uint);
            var __y__ = default(uint);
            var __z__ = default(uint);

            for (int i = 0; i < length; i++)
            {
                switch (i)
                {
                    case 0:
                        __x__ = reader.ReadUInt32();
                        break;
                    case 1:
                        __y__ = reader.ReadUInt32();
                        break;
                    case 2:
                        __z__ = reader.ReadUInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new global::SignalStreaming.Quantization.QuantizedVector3(__x__, __y__, __z__);
            reader.Depth--;
            return ____result;
        }
    }

    public sealed class QuantizedVector4Formatter : global::MessagePack.Formatters.IMessagePackFormatter<global::SignalStreaming.Quantization.QuantizedVector4>
    {
        public void Serialize(ref global::MessagePack.MessagePackWriter writer, global::SignalStreaming.Quantization.QuantizedVector4 value, global::MessagePack.MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(4);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        public global::SignalStreaming.Quantization.QuantizedVector4 Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                throw new global::System.InvalidOperationException("typecode is null, struct not supported");
            }

            options.Security.DepthStep(ref reader);
            var length = reader.ReadArrayHeader();
            var __x__ = default(uint);
            var __y__ = default(uint);
            var __z__ = default(uint);
            var __w__ = default(uint);

            for (int i = 0; i < length; i++)
            {
                switch (i)
                {
                    case 0:
                        __x__ = reader.ReadUInt32();
                        break;
                    case 1:
                        __y__ = reader.ReadUInt32();
                        break;
                    case 2:
                        __z__ = reader.ReadUInt32();
                        break;
                    case 3:
                        __w__ = reader.ReadUInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new global::SignalStreaming.Quantization.QuantizedVector4(__x__, __y__, __z__, __w__);
            reader.Depth--;
            return ____result;
        }
    }

    public sealed class QuantizedQuaternionFormatter : global::MessagePack.Formatters.IMessagePackFormatter<global::SignalStreaming.Quantization.QuantizedQuaternion>
    {
        public void Serialize(ref global::MessagePack.MessagePackWriter writer, global::SignalStreaming.Quantization.QuantizedQuaternion value, global::MessagePack.MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(4);
            writer.Write(value.m);
            writer.Write(value.a);
            writer.Write(value.b);
            writer.Write(value.c);
        }

        public global::SignalStreaming.Quantization.QuantizedQuaternion Deserialize(ref global::MessagePack.MessagePackReader reader, global::MessagePack.MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                throw new global::System.InvalidOperationException("typecode is null, struct not supported");
            }

            options.Security.DepthStep(ref reader);
            var length = reader.ReadArrayHeader();
            var __m__ = default(uint);
            var __a__ = default(uint);
            var __b__ = default(uint);
            var __c__ = default(uint);

            for (int i = 0; i < length; i++)
            {
                switch (i)
                {
                    case 0:
                        __m__ = reader.ReadUInt32();
                        break;
                    case 1:
                        __a__ = reader.ReadUInt32();
                        break;
                    case 2:
                        __b__ = reader.ReadUInt32();
                        break;
                    case 3:
                        __c__ = reader.ReadUInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            var ____result = new global::SignalStreaming.Quantization.QuantizedQuaternion(__m__, __a__, __b__, __c__);
            reader.Depth--;
            return ____result;            
        }
    }
}

#pragma warning restore 168
#pragma warning restore 414
#pragma warning restore 618
#pragma warning restore 612

#pragma warning restore SA1129 // Do not use default value type constructor
#pragma warning restore SA1309 // Field names should not begin with underscore
#pragma warning restore SA1312 // Variable names should begin with lower-case letter
#pragma warning restore SA1403 // File may only contain a single namespace
#pragma warning restore SA1649 // File name should match first type name
