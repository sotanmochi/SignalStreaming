using System.Buffers;
using NetStack.Quantization;
using NetStack.Serialization;

namespace SignalStreaming.Serialization
{
    public sealed partial class SignalSerializer : ISignalSerializer
    {
        readonly BitBuffer _bitBuffer = new(256); // ChunkCount: 256, BufferSize: 256 * 4 = 1024 [bytes]

        void Serialize(IBufferWriter<byte> writer, QuantizedVector3 value)
        {
            var span = writer.GetSpan();

            _bitBuffer.Clear();
            var length = _bitBuffer
                            .AddUInt(value.x)
                            .AddUInt(value.y)
                            .AddUInt(value.z)
                            .ToSpan(ref span);

            writer.Advance(length);
        }

        void Serialize(IBufferWriter<byte> writer, QuantizedQuaternion value)
        {
            var span = writer.GetSpan();

            _bitBuffer.Clear();
            var length = _bitBuffer
                            .AddUInt(value.m)
                            .AddUInt(value.a)
                            .AddUInt(value.b)
                            .AddUInt(value.c)
                            .ToSpan(ref span);

            writer.Advance(length);
        }

        QuantizedVector3 DeserializeQuantizedVector3(in ReadOnlySequence<byte> byteSequence)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            _bitBuffer.Clear();
            _bitBuffer.FromSpan(ref span, (int)byteSequence.Length);
            // _bitBuffer.FromSpan(ref span, span.Length);

            // TODO: Multisegments

            var x = _bitBuffer.ReadUInt();
            var y = _bitBuffer.ReadUInt();
            var z = _bitBuffer.ReadUInt();

            return new QuantizedVector3(x, y, z);
        }

        QuantizedQuaternion DeserializeQuantizedQuaternion(in ReadOnlySequence<byte> byteSequence)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            _bitBuffer.Clear();
            _bitBuffer.FromSpan(ref span, (int)byteSequence.Length);
            // _bitBuffer.FromSpan(ref span, span.Length);

            // TODO: Multisegments

            var m = _bitBuffer.ReadUInt();
            var a = _bitBuffer.ReadUInt();
            var b = _bitBuffer.ReadUInt();
            var c = _bitBuffer.ReadUInt();

            return new QuantizedQuaternion(m, a, b, c);
        }
    }
}