using System;
using System.Buffers;
using SignalStreaming.Quantization;
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

        void Serialize(IBufferWriter<byte> writer, QuantizedVector value)
        {
            if (value.Size > _bitBuffer.Length)
            {
                throw new ArgumentException($"QuantizedVector size {value.Size} is greater than the buffer size {_bitBuffer.Length}");
            }

            var span = writer.GetSpan();

            _bitBuffer.Clear();
            _bitBuffer.AddInt(value.Size);
            for (var i = 0; i < value.Size; i++)
            {
                _bitBuffer.AddUInt(value.Elements[i]);
            }

            var length = _bitBuffer.ToSpan(ref span);
            writer.Advance(length);
        }

        QuantizedVector DeserializeQuantizedVector(in ReadOnlySequence<byte> byteSequence)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            _bitBuffer.Clear();
            _bitBuffer.FromSpan(ref span, (int)byteSequence.Length);

            // TODO: Multisegments

            var size = _bitBuffer.ReadInt();
            var quantizedVector = new QuantizedVector(size); // Allocation

            for (var i = 0; i < size; i++)
            {
                quantizedVector.Elements[i] = _bitBuffer.ReadUInt();
            }

            return quantizedVector;
        }

        void DeserializeToQuantizedVector(in ReadOnlySequence<byte> byteSequence, QuantizedVector output)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            _bitBuffer.Clear();
            _bitBuffer.FromSpan(ref span, (int)byteSequence.Length);

            // TODO: Multisegments

            var size = _bitBuffer.ReadInt();
            if (size != output.Size)
            {
                _bitBuffer.Clear();
                throw new ArgumentException("Mismatched size");
            }

            for (var i = 0; i < size; i++)
            {
                output.Elements[i] = _bitBuffer.ReadUInt();
            }
        }

        void Serialize(IBufferWriter<byte> writer, QuantizedHumanPose value)
        {
            _bitBuffer.Clear();

            // BodyPosition
            _bitBuffer.AddUInt(value.BodyPosition.x)
                        .AddUInt(value.BodyPosition.y)
                        .AddUInt(value.BodyPosition.z);

            // BodyRotation
            _bitBuffer.AddUInt(value.BodyRotation.m)
                        .AddUInt(value.BodyRotation.a)
                        .AddUInt(value.BodyRotation.b)
                        .AddUInt(value.BodyRotation.c);

            // Muscles
            _bitBuffer.AddInt(value.Muscles.Size);
            for (var i = 0; i < value.Muscles.Size; i++)
            {
                _bitBuffer.AddUInt(value.Muscles.Elements[i]);
            }

            var span = writer.GetSpan();
            var length = _bitBuffer.ToSpan(ref span);
            writer.Advance(length);
        }

        QuantizedHumanPose DeserializeQuantizedHumanPose(in ReadOnlySequence<byte> byteSequence)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            _bitBuffer.Clear();
            _bitBuffer.FromSpan(ref span, (int)byteSequence.Length);

            // TODO: Multisegments

            var x = _bitBuffer.ReadUInt();
            var y = _bitBuffer.ReadUInt();
            var z = _bitBuffer.ReadUInt();

            var m = _bitBuffer.ReadUInt();
            var a = _bitBuffer.ReadUInt();
            var b = _bitBuffer.ReadUInt();
            var c = _bitBuffer.ReadUInt();

            var muscleCount = _bitBuffer.ReadInt();

            var quantizedHumanPose = new QuantizedHumanPose(muscleCount); // Allocation
            for (var i = 0; i < muscleCount; i++)
            {
                quantizedHumanPose.Muscles.Elements[i] = _bitBuffer.ReadUInt();
            }
            quantizedHumanPose.BodyPosition = new QuantizedVector3(x, y, z);
            quantizedHumanPose.BodyRotation = new QuantizedQuaternion(m, a, b, c);

            return quantizedHumanPose;
        }

        void DeserializeToQuantizedHumanPose(in ReadOnlySequence<byte> byteSequence, QuantizedHumanPose output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            _bitBuffer.Clear();
            _bitBuffer.FromSpan(ref span, (int)byteSequence.Length);

            // TODO: Multisegments

            var x = _bitBuffer.ReadUInt();
            var y = _bitBuffer.ReadUInt();
            var z = _bitBuffer.ReadUInt();

            var m = _bitBuffer.ReadUInt();
            var a = _bitBuffer.ReadUInt();
            var b = _bitBuffer.ReadUInt();
            var c = _bitBuffer.ReadUInt();

            var muscleCount = _bitBuffer.ReadInt();
            if (muscleCount != output.Muscles.Size)
            {
                _bitBuffer.Clear();
                throw new ArgumentException("Mismatched muscle count");
            }

            for (var i = 0; i < muscleCount; i++)
            {
                output.Muscles.Elements[i] = _bitBuffer.ReadUInt();
            }
            output.BodyPosition = new QuantizedVector3(x, y, z);
            output.BodyRotation = new QuantizedQuaternion(m, a, b, c);
        }
    }
}