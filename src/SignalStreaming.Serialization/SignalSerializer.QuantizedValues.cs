using System;
using System.Buffers;
using NetStack.Quantization;
using NetStack.Serialization;
using SignalStreaming.Quantization;

namespace SignalStreaming.Serialization
{
    public sealed partial class SignalSerializer : ISignalSerializer
    {
        readonly BitBuffer _bitBuffer = new(256); // ChunkCount: 256, BufferSize: 256 * 4 = 1024 [bytes]

        public void Serialize(IBufferWriter<byte> writer, QuantizedVector3 value, int[] requiredBits, int[] deltaRequiredBits, VectorQuantizer.DeltaType deltaType)
        {
            var span = writer.GetSpan();
            var numElements = 3;
            var requiredBitsX = 0;
            var requiredBitsY = 0;
            var requiredBitsZ = 0;

            if (deltaType == VectorQuantizer.DeltaType.OutOfDeltaRange)
            {
                requiredBitsX = requiredBits[0];
                requiredBitsY = requiredBits[1];
                requiredBitsZ = requiredBits[2];
            }
            else if (deltaType == VectorQuantizer.DeltaType.XYZ)
            {
                requiredBitsX = deltaRequiredBits[0];
                requiredBitsY = deltaRequiredBits[1];
                requiredBitsZ = deltaRequiredBits[2];
            }
            else if (deltaType == VectorQuantizer.DeltaType.XY)
            {
                requiredBitsX = deltaRequiredBits[0];
                requiredBitsY = deltaRequiredBits[1];
                requiredBitsZ = requiredBits[2];
            }
            else if (deltaType == VectorQuantizer.DeltaType.XZ)
            {
                requiredBitsX = deltaRequiredBits[0];
                requiredBitsY = requiredBits[1];
                requiredBitsZ = deltaRequiredBits[2];
            }
            else if (deltaType == VectorQuantizer.DeltaType.YZ)
            {
                requiredBitsX = requiredBits[0];
                requiredBitsY = deltaRequiredBits[1];
                requiredBitsZ = deltaRequiredBits[2];
            }
            else if (deltaType == VectorQuantizer.DeltaType.X)
            {
                requiredBitsX = deltaRequiredBits[0];
                requiredBitsY = requiredBits[1];
                requiredBitsZ = requiredBits[2];
            }
            else if (deltaType == VectorQuantizer.DeltaType.Y)
            {
                requiredBitsX = requiredBits[0];
                requiredBitsY = deltaRequiredBits[1];
                requiredBitsZ = requiredBits[2];
            }
            else if (deltaType == VectorQuantizer.DeltaType.Z)
            {
                requiredBitsX = requiredBits[0];
                requiredBitsY = requiredBits[1];
                requiredBitsZ = deltaRequiredBits[2];
            }
            else
            {
                throw new InvalidOperationException($"DeltaType {deltaType} is not supported.");
            }

            if ((byte)deltaType > 0xF)
            {
                throw new InvalidOperationException($"DeltaType {deltaType} is not supported.");
            }

            _bitBuffer.Clear();
            var packedData = (byte)((numElements & 0xF) << 4 | ((byte)deltaType & 0xF));
            var length = _bitBuffer
                .Add(8, packedData)
                .Add(requiredBitsX, value.x)
                .Add(requiredBitsY, value.y)
                .Add(requiredBitsZ, value.z)
                .ToSpan(ref span);

            writer.Advance(length);
        }

        public void DeserializeQuantizedVector(in ReadOnlySequence<byte> byteSequence,
            int[] requiredBits, int[] deltaRequiredBits, out VectorQuantizer.DeltaType deltaType, uint[] outputVectorElements)
        {
            var reader = new SequenceReader<byte>(byteSequence);
            var span = reader.CurrentSpan;

            _bitBuffer.Clear();
            _bitBuffer.FromSpan(ref span, (int)byteSequence.Length);

            var packedData = _bitBuffer.ReadByte();

            var numElements = packedData >> 4;
            if (outputVectorElements == null || outputVectorElements.Length != numElements)
            {
                throw new InvalidOperationException($"Vector elements array must be initialized and have a length of {numElements}.");
            }

            deltaType = (VectorQuantizer.DeltaType)(packedData & 0xF);

            if (deltaType == VectorQuantizer.DeltaType.OutOfDeltaRange)
            {
                outputVectorElements[0] = _bitBuffer.Read(requiredBits[0]);
                outputVectorElements[1] = _bitBuffer.Read(requiredBits[1]);
                outputVectorElements[2] = _bitBuffer.Read(requiredBits[2]);
            }
            else if (deltaType == VectorQuantizer.DeltaType.XYZ)
            {
                outputVectorElements[0] = _bitBuffer.Read(deltaRequiredBits[0]);
                outputVectorElements[1] = _bitBuffer.Read(deltaRequiredBits[1]);
                outputVectorElements[2] = _bitBuffer.Read(deltaRequiredBits[2]);
            }
            else if (deltaType == VectorQuantizer.DeltaType.XY)
            {
                outputVectorElements[0] = _bitBuffer.Read(deltaRequiredBits[0]);
                outputVectorElements[1] = _bitBuffer.Read(deltaRequiredBits[1]);
                outputVectorElements[2] = _bitBuffer.Read(requiredBits[2]);
            }
            else if (deltaType == VectorQuantizer.DeltaType.XZ)
            {
                outputVectorElements[0] = _bitBuffer.Read(deltaRequiredBits[0]);
                outputVectorElements[1] = _bitBuffer.Read(requiredBits[1]);
                outputVectorElements[2] = _bitBuffer.Read(deltaRequiredBits[2]);
            }
            else if (deltaType == VectorQuantizer.DeltaType.YZ)
            {
                outputVectorElements[0] = _bitBuffer.Read(requiredBits[0]);
                outputVectorElements[1] = _bitBuffer.Read(deltaRequiredBits[1]);
                outputVectorElements[2] = _bitBuffer.Read(deltaRequiredBits[2]);
            }
            else if (deltaType == VectorQuantizer.DeltaType.X)
            {
                outputVectorElements[0] = _bitBuffer.Read(deltaRequiredBits[0]);
                outputVectorElements[1] = _bitBuffer.Read(requiredBits[1]);
                outputVectorElements[2] = _bitBuffer.Read(requiredBits[2]);
            }
            else if (deltaType == VectorQuantizer.DeltaType.Y)
            {
                outputVectorElements[0] = _bitBuffer.Read(requiredBits[0]);
                outputVectorElements[1] = _bitBuffer.Read(deltaRequiredBits[1]);
                outputVectorElements[2] = _bitBuffer.Read(requiredBits[2]);
            }
            else if (deltaType == VectorQuantizer.DeltaType.Z)
            {
                outputVectorElements[0] = _bitBuffer.Read(requiredBits[0]);
                outputVectorElements[1] = _bitBuffer.Read(requiredBits[1]);
                outputVectorElements[2] = _bitBuffer.Read(deltaRequiredBits[2]);
            }
            else
            {
                throw new InvalidOperationException($"DeltaType {deltaType} is not supported.");
            }
        }

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