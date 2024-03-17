using System;
using System.Runtime.InteropServices;
using MessagePack;
using MessagePack.Resolvers;
using NetStack.Quantization;
using NetStack.Serialization;
using SignalStreaming.Quantization;
using SignalStreaming.Serialization.MessagePack.NetStack;
using UnityEngine;

namespace SignalStreaming.SerializationTest
{
    class Program
    {
        public const float Epsilon = 0.000001f; // 1e-6
        public const float Rad2Deg = 360f / ((float)Math.PI * 2);

        static void Main(string[] args)
        {
            UnionValueTest();
            QuantizedQuaternionTest();
            QuantizedQuaternionTest2();
            QuantizedPositionTest();
            QuantizedPositionTest2();
            QuantizedFloatArrayTest();
            VectorQuantizerTest();
        }

        static void UnionValueTest()
        {
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"        UnionValueTest");
            Console.WriteLine("----------------------------------------");
            var value = new UnionValue();
            value = -1.234f;
            Console.WriteLine($"[{nameof(UnionValueTest)}] Size of UnionValue: {Marshal.SizeOf(value)} bytes");
            Console.WriteLine($"[{nameof(UnionValueTest)}] Value.AsFloat(): {value.AsFloat()}");
            Console.WriteLine($"[{nameof(UnionValueTest)}] Value.AsInt(): {value.AsInt()}");
            Console.WriteLine($"[{nameof(UnionValueTest)}] Value.AsUInt(): {value.AsUInt()}");
            Console.WriteLine("----------------------------------------");
        }

        static void QuantizedQuaternionTest()
        {
            var options = MessagePackSerializerOptions.Standard
                            .WithResolver(CompositeResolver.Create(QuantizationResolver.Instance, StandardResolver.Instance));

            var q = GenerateRandomQuaternion();
            var qq = SmallestThree.Quantize(q.ToSystemNumericsQuaternion());

            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"        QuantizedQuaternionTest");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest)}] Size of Quaternion: {Marshal.SizeOf(q)} bytes");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest)}] Size of QuantizedQuaternion: {Marshal.SizeOf(qq)} bytes");
            Console.WriteLine("");

            var serializedQ = MessagePackSerializer.Serialize(q, options);
            var serializedQq = MessagePackSerializer.Serialize(qq, options);

            Console.WriteLine($"[{nameof(QuantizedQuaternionTest)}] Serialized data size of Quaternion: {serializedQ.Length} bytes");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest)}] Serialized data size of QuantizedQuaternion: {serializedQq.Length} bytes");
            Console.WriteLine("");

            var deserializedQq = MessagePackSerializer.Deserialize<QuantizedQuaternion>(serializedQq, options);
            var dequantizedQ = SmallestThree.Dequantize(deserializedQq).ToUnityQuaternion();
            var angle = Angle(q, dequantizedQ);

            Console.WriteLine($"[{nameof(QuantizedQuaternionTest)}] q: ({q.x}, {q.y}, {q.z}, {q.w})");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest)}] dequantizedQ: ({dequantizedQ.x}, {dequantizedQ.y}, {dequantizedQ.z}, {dequantizedQ.w})");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest)}] Dot(q, dequantizedQ): {Dot(q, dequantizedQ)}");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest)}] Angle(q, dequantizedQ): {angle} [deg]");
            Console.WriteLine("----------------------------------------");
        }

        static void QuantizedQuaternionTest2()
        {
            var q = GenerateRandomQuaternion();
            var qq = SmallestThree.Quantize(q.ToSystemNumericsQuaternion());

            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"        QuantizedQuaternionTest2");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest2)}] Size of Quaternion: {Marshal.SizeOf(q)} bytes");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest2)}] Size of QuantizedQuaternion: {Marshal.SizeOf(qq)} bytes");
            Console.WriteLine("");

            var bytesQ = new byte[1500];
            var bytesSpanQ = new Span<byte>(bytesQ);

            var bytesQq = new byte[1500];
            var bytesSpanQq = new Span<byte>(bytesQq);

            var bitButterQ = new BitBuffer(375); // ChunkCount: 375, BufferSize: 375 * 4 = 1500 bytes
            var bitButterQq = new BitBuffer(375); // ChunkCount: 375, BufferSize: 375 * 4 = 1500 bytes

            var serializedQLength = bitButterQ
                                        .AddUInt(new UnionValue(q.x).AsUInt())
                                        .AddUInt(new UnionValue(q.y).AsUInt())
                                        .AddUInt(new UnionValue(q.z).AsUInt())
                                        .AddUInt(new UnionValue(q.w).AsUInt())
                                        .ToSpan(ref bytesSpanQ);
            var serializedQqLength = bitButterQq
                                        .AddUInt(qq.m)
                                        .AddUInt(qq.a)
                                        .AddUInt(qq.b)
                                        .AddUInt(qq.c)
                                        .ToSpan(ref bytesSpanQq);

            Console.WriteLine($"[{nameof(QuantizedQuaternionTest2)}] Serialized data size of Quaternion: {serializedQLength} bytes");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest2)}] Serialized data size of QuantizedQuaternion: {serializedQqLength} bytes");
            Console.WriteLine("");

            var bytesQqReadOnlySpan = new ReadOnlySpan<byte>(bytesQq);
            var bitBuffer = new BitBuffer(375); // ChunkCount: 375, BufferSize: 375 * 4 = 1500 bytes
            bitBuffer.FromSpan(ref bytesQqReadOnlySpan, serializedQqLength);

            var deserializedQq = new QuantizedQuaternion(bitBuffer.ReadUInt(), bitBuffer.ReadUInt(), bitBuffer.ReadUInt(), bitBuffer.ReadUInt());
            var dequantizedQ = SmallestThree.Dequantize(deserializedQq).ToUnityQuaternion();
            var angle = Angle(q, dequantizedQ);

            Console.WriteLine($"[{nameof(QuantizedQuaternionTest2)}] q: ({q.x}, {q.y}, {q.z}, {q.w})");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest2)}] dequantizedQ: ({dequantizedQ.x}, {dequantizedQ.y}, {dequantizedQ.z}, {dequantizedQ.w})");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest2)}] Dot(q, dequantizedQ): {Dot(q, dequantizedQ)}");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest2)}] Angle(q, dequantizedQ): {angle} [deg]");
            Console.WriteLine("----------------------------------------");
        }

        static void QuantizedPositionTest()
        {
            var options = MessagePackSerializerOptions.Standard
                            .WithResolver(CompositeResolver.Create(QuantizationResolver.Instance, StandardResolver.Instance));

            var precision = 0.001f;
            var worldBounds = new BoundedRange[3];
            worldBounds[0] = new BoundedRange(-256.0f, 256.0f, precision);
            worldBounds[1] = new BoundedRange(-256.0f, 256.0f, precision);
            worldBounds[2] = new BoundedRange(-32.0f, 32.0f, precision);

            var random = new Random();
            var posX = random.NextSingle() * 512.0f - 256.0f; // [-256, 256]
            var posY = random.NextSingle() * 512.0f - 256.0f; // [-256, 256]
            var posZ = random.NextSingle() * 64.0f - 32.0f; // [-32, 32]

            var position = new Vector3(posX, posY, posZ);
            var quantizedPosition = BoundedRange.Quantize(position.ToSystemNumericsVector3(), worldBounds);

            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"        QuantizedPositionTest");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"[{nameof(QuantizedPositionTest)}] Size of Position: {Marshal.SizeOf(position)} bytes");
            Console.WriteLine($"[{nameof(QuantizedPositionTest)}] Size of QuantizedPosition: {Marshal.SizeOf(quantizedPosition)} bytes");
            Console.WriteLine("");

            var serializedV = MessagePackSerializer.Serialize(position, options);
            var serializedQv = MessagePackSerializer.Serialize(quantizedPosition, options);

            Console.WriteLine($"[{nameof(QuantizedPositionTest)}] Serialized data size of Vector3: {serializedV.Length} bytes");
            Console.WriteLine($"[{nameof(QuantizedPositionTest)}] Serialized data size of QuantizedVector3: {serializedQv.Length} bytes");
            Console.WriteLine("");

            var deserializedQv = MessagePackSerializer.Deserialize<QuantizedVector3>(serializedQv, options);
            var dequantizedQ = BoundedRange.Dequantize(deserializedQv, worldBounds);
            var dequantizedPosition = dequantizedQ.ToUnityVector3();

            Console.WriteLine($"[{nameof(QuantizedPositionTest)}] position: ({position.x}, {position.y}, {position.z})");
            Console.WriteLine($"[{nameof(QuantizedPositionTest)}] dequantizedPosition: ({dequantizedPosition.x}, {dequantizedPosition.y}, {dequantizedPosition.z})");

            var diffX = Math.Abs(position.x - dequantizedPosition.x);
            var diffY = Math.Abs(position.y - dequantizedPosition.y);
            var diffZ = Math.Abs(position.z - dequantizedPosition.z);
            Console.WriteLine($"[{nameof(QuantizedPositionTest)}] diff: ({diffX}, {diffY}, {diffZ})");
            Console.WriteLine($"[{nameof(QuantizedPositionTest)}] precision: ({precision}, {precision}, {precision})");
            
            var approximately = (Math.Abs(diffX) < precision) && (Math.Abs(diffY) < precision) && (Math.Abs(diffZ) < precision);
            Console.WriteLine($"[{nameof(QuantizedPositionTest)}] approximately: {approximately}");

            Console.WriteLine("----------------------------------------");
        }

        static void QuantizedPositionTest2()
        {
            var precision = 0.001f;
            var worldBounds = new BoundedRange[3];
            worldBounds[0] = new BoundedRange(-64f, 64f, precision); // X
            worldBounds[1] = new BoundedRange(-16f, 48f, precision); // Y (Height)
            worldBounds[2] = new BoundedRange(-64f, 64f, precision); // Z

            var random = new Random();
            var posX = random.NextSingle() * 128.0f - 64.0f; // [-64, 64]
            var posY = random.NextSingle() * 64.0f - 16.0f; // [-16, 48]
            var posZ = random.NextSingle() * 128.0f - 64.0f; // [-64, 64]

            var position = new Vector3(posX, posY, posZ);
            var quantizedPosition = BoundedRange.Quantize(position.ToSystemNumericsVector3(), worldBounds);

            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"        QuantizedPositionTest2");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"[{nameof(QuantizedPositionTest2)}] Size of Position: {Marshal.SizeOf(position)} bytes");
            Console.WriteLine($"[{nameof(QuantizedPositionTest2)}] Size of QuantizedPosition: {Marshal.SizeOf(quantizedPosition)} bytes");
            Console.WriteLine("");

            var bytesV = new byte[1500];
            var bytesSpanV = new Span<byte>(bytesV);

            var bytesQv = new byte[1500];
            var bytesSpanQv = new Span<byte>(bytesQv);

            var bitButterV = new BitBuffer(375); // ChunkCount: 375, BufferSize: 375 * 4 = 1500 bytes
            var bitButterQv = new BitBuffer(375); // ChunkCount: 375, BufferSize: 375 * 4 = 1500 bytes

            var serializedVLength = bitButterV
                                        .AddUInt(new UnionValue(position.x).AsUInt())
                                        .AddUInt(new UnionValue(position.y).AsUInt())
                                        .AddUInt(new UnionValue(position.z).AsUInt())
                                        .ToSpan(ref bytesSpanV);
            var serializedQvLength = bitButterQv
                                        .AddUInt(quantizedPosition.x)
                                        .AddUInt(quantizedPosition.y)
                                        .AddUInt(quantizedPosition.z)
                                        .ToSpan(ref bytesSpanQv);

            Console.WriteLine($"[{nameof(QuantizedPositionTest2)}] Serialized data size of Vector3: {serializedVLength} bytes");
            Console.WriteLine($"[{nameof(QuantizedPositionTest2)}] Serialized data size of QuantizedVector3: {serializedQvLength} bytes");
            Console.WriteLine("");

            var bytesQvReadOnlySpan = new ReadOnlySpan<byte>(bytesQv);
            var bitBuffer = new BitBuffer(375); // ChunkCount: 375, BufferSize: 375 * 4 = 1500 bytes
            bitBuffer.FromSpan(ref bytesQvReadOnlySpan, serializedQvLength);

            var deserializedQv = new QuantizedVector3(bitBuffer.ReadUInt(), bitBuffer.ReadUInt(), bitBuffer.ReadUInt());
            var dequantizedQ = BoundedRange.Dequantize(deserializedQv, worldBounds);
            var dequantizedPosition = dequantizedQ.ToUnityVector3();

            Console.WriteLine($"[{nameof(QuantizedPositionTest2)}] position: ({position.x}, {position.y}, {position.z})");
            Console.WriteLine($"[{nameof(QuantizedPositionTest2)}] dequantizedPosition: ({dequantizedPosition.x}, {dequantizedPosition.y}, {dequantizedPosition.z})");

            var diffX = Math.Abs(position.x - dequantizedPosition.x);
            var diffY = Math.Abs(position.y - dequantizedPosition.y);
            var diffZ = Math.Abs(position.z - dequantizedPosition.z);
            Console.WriteLine($"[{nameof(QuantizedPositionTest2)}] diff: ({diffX}, {diffY}, {diffZ})");
            Console.WriteLine($"[{nameof(QuantizedPositionTest2)}] precision: ({precision}, {precision}, {precision})");

            var approximately = (Math.Abs(diffX) < precision) && (Math.Abs(diffY) < precision) && (Math.Abs(diffZ) < precision);
            Console.WriteLine($"[{nameof(QuantizedPositionTest2)}] approximately: {approximately}");

            Console.WriteLine("----------------------------------------");
        }

        static void QuantizedFloatArrayTest()
        {
            var precision = 0.1f;
            var boundedRange = new BoundedRange(-180.0f, 180.0f, precision);
            var mask = (uint)((1L << boundedRange.RequiredBits) - 1);

            var random = new Random();

            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"        QuantizedFloatArrayTest");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"[{nameof(QuantizedFloatArrayTest)}] MinValue: {boundedRange.MinValue}, MaxValue: {boundedRange.MaxValue}, Precision: {precision}");
            Console.WriteLine($"[{nameof(QuantizedFloatArrayTest)}] RequiredBits: {boundedRange.RequiredBits} bits");
            Console.WriteLine($"[{nameof(QuantizedFloatArrayTest)}] Mask: {Convert.ToString(mask, 16).ToUpper()} ({Convert.ToString(mask, 2)}))"); 
            Console.WriteLine("");

            var bitButter = new BitBuffer(256); // ChunkCount: 256, BufferSize: 256 * 4 = 1024 bytes
            var bytes = new byte[1024];
            var bytesSpan = new Span<byte>(bytes);

            var floatArray = new float[95];
            bitButter.AddByte((byte)floatArray.Length);
            bitButter.AddByte((byte)boundedRange.RequiredBits);

            for (var i = 0; i < floatArray.Length; i++)
            {
                floatArray[i] = random.NextSingle() * 180.0f - 180.0f;
                bitButter.Add(boundedRange.RequiredBits, boundedRange.Quantize(floatArray[i]));
            }

            var serializedLength = bitButter.ToSpan(ref bytesSpan);

            var bytesReadOnlySpan = new ReadOnlySpan<byte>(bytes);
            bitButter.Clear();
            bitButter.FromSpan(ref bytesReadOnlySpan, serializedLength);

            var arrayLength = bitButter.ReadByte();
            var requiredBits = bitButter.ReadByte();
            var dequantizedFloatArray = new float[arrayLength];
            for (var i = 0; i < arrayLength; i++)
            {
                var quantizedValue = bitButter.Read(requiredBits);
                dequantizedFloatArray[i] = boundedRange.Dequantize(quantizedValue);
            }

            var maxDiff = 0.0f;
            for (var i = 0; i < floatArray.Length; i++)
            {
                var diff = Math.Abs(floatArray[i] - dequantizedFloatArray[i]);
                maxDiff = Math.Max(maxDiff, diff);
            }

            Console.WriteLine($"[{nameof(QuantizedFloatArrayTest)}] Serialized data size of QuantizedFloatArray: {serializedLength} bytes");
            Console.WriteLine($"[{nameof(QuantizedFloatArrayTest)}] ArrayLength: {arrayLength}");
            Console.WriteLine($"[{nameof(QuantizedFloatArrayTest)}] Max diff: {maxDiff}");
            Console.WriteLine($"[{nameof(QuantizedFloatArrayTest)}] Precision: {precision}");
            Console.WriteLine("----------------------------------------");
        }

        static void VectorQuantizerTest()
        {
            var precision = 0.001f;
            var worldBounds = new BoundedRange[3];
            worldBounds[0] = new BoundedRange(-64f, 64f, precision); // X
            worldBounds[1] = new BoundedRange(-16f, 48f, precision); // Y (Height)
            worldBounds[2] = new BoundedRange(-64f, 64f, precision); // Z

            var quantizer = new VectorQuantizer(worldBounds);

            var random = new Random();
            var posX = random.NextSingle() * 128.0f - 64.0f; // [-64, 64]
            var posY = random.NextSingle() * 64.0f - 16.0f; // [-16, 48]
            var posZ = random.NextSingle() * 128.0f - 64.0f; // [-64, 64]

            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"        VectorQuantizerTest");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] MinValues: {worldBounds[0].MinValue}, {worldBounds[1].MinValue}, {worldBounds[2].MinValue}");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] MaxValues: {worldBounds[0].MaxValue}, {worldBounds[1].MaxValue}, {worldBounds[2].MaxValue}");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] Precision: {precision}");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] RequiredBits: {worldBounds[0].RequiredBits}, {worldBounds[1].RequiredBits}, {worldBounds[2].RequiredBits}");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] RequiredBytes: {Math.Ceiling(worldBounds[0].RequiredBits / 8.0)}, {Math.Ceiling(worldBounds[1].RequiredBits / 8.0)}, {Math.Ceiling(worldBounds[2].RequiredBits / 8.0)}");
            Console.WriteLine("");
            // Console.WriteLine($"[{nameof(VectorQuantizerTest)}] DeltaMinValues: {quantizer.DeltaMinValues[0]}, {quantizer.DeltaMinValues[1]}, {quantizer.DeltaMinValues[2]}");
            // Console.WriteLine($"[{nameof(VectorQuantizerTest)}] DeltaMaxValues: {quantizer.DeltaMaxValues[0]}, {quantizer.DeltaMaxValues[1]}, {quantizer.DeltaMaxValues[2]}");
            // Console.WriteLine($"[{nameof(VectorQuantizerTest)}] DeltaPrecision: {quantizer.DeltaPrecision}");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] DeltaRequiredBits: {quantizer.DeltaRequiredBits[0]}, {quantizer.DeltaRequiredBits[1]}, {quantizer.DeltaRequiredBits[2]}");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] DeltaRequiredBytes: {Math.Ceiling(quantizer.DeltaRequiredBits[0] / 8.0)}, {Math.Ceiling(quantizer.DeltaRequiredBits[1] / 8.0)}, {Math.Ceiling(quantizer.DeltaRequiredBits[2] / 8.0)}");
            Console.WriteLine("");

            var bitButter = new BitBuffer(256); // ChunkCount: 256, BufferSize: 256 * 4 = 1024 bytes
            var bytes = new byte[1024];
            var bytesSpan = new Span<byte>(bytes);

            var position = new Vector3(posX, posY, posZ);
            var previousPosition = position;
            var requiredBitsOutput = new int[3];
            quantizer.Quantize(position.ToSystemNumericsVector3(), out QuantizedVector3 quantizedValue, out VectorQuantizer.DeltaType deltaType, requiredBitsOutput);

            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] Position: ({position.x}, {position.y}, {position.z})");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] PreviousPosition: ({previousPosition.x}, {previousPosition.y}, {previousPosition.z})");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] Diff: ({position.x - previousPosition.x}, {position.y - previousPosition.y}, {position.z - previousPosition.z})");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] DeltaType: {deltaType}");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] RequiredBits: {requiredBitsOutput[0]}, {requiredBitsOutput[1]}, {requiredBitsOutput[2]}");
            Console.WriteLine($"[{nameof(VectorQuantizerTest)}] RequiredBytes: {Math.Ceiling(requiredBitsOutput[0] / 8.0)}, {Math.Ceiling(requiredBitsOutput[1] / 8.0)}, {Math.Ceiling(requiredBitsOutput[2] / 8.0)}");
            Console.WriteLine("");

            for (var i = 0; i < 10; i++)
            {
                var dx = random.NextSingle() * 1f - 0.5f;
                var dy = random.NextSingle() * 1f - 0.5f;
                var dz = random.NextSingle() * 1f - 0.5f;

                position.x += dx;
                position.y += dy;
                position.z += dz;
                quantizer.Quantize(position.ToSystemNumericsVector3(), out quantizedValue, out deltaType, requiredBitsOutput);
                quantizer.Dequantize(new uint[]{ quantizedValue.x, quantizedValue.y, quantizedValue.z }, deltaType, out System.Numerics.Vector3 dequantizedValue);
            
                var bytesLength = (byte)Math.Ceiling((requiredBitsOutput[0] + requiredBitsOutput[1] + requiredBitsOutput[2]) / 8f);
                byte packed = (byte)((bytesLength & 0x1F) << 3 | ((byte)deltaType & 0x7));

                Console.WriteLine($"[{nameof(VectorQuantizerTest)}] Position: ({position.x}, {position.y}, {position.z})");
                Console.WriteLine($"[{nameof(VectorQuantizerTest)}] PreviousPosition: ({previousPosition.x}, {previousPosition.y}, {previousPosition.z})");
                Console.WriteLine($"[{nameof(VectorQuantizerTest)}] Diff: ({position.x - previousPosition.x}, {position.y - previousPosition.y}, {position.z - previousPosition.z})");
                Console.WriteLine($"[{nameof(VectorQuantizerTest)}] Delta: ({dx}, {dy}, {dz})");
                Console.WriteLine($"[{nameof(VectorQuantizerTest)}] DeltaType: {deltaType}");
                Console.WriteLine($"[{nameof(VectorQuantizerTest)}] DequantizedValue: ({dequantizedValue.X}, {dequantizedValue.Y}, {dequantizedValue.Z})");
                Console.WriteLine($"[{nameof(VectorQuantizerTest)}] RequiredBits: {requiredBitsOutput[0]}, {requiredBitsOutput[1]}, {requiredBitsOutput[2]}");
                Console.WriteLine($"[{nameof(VectorQuantizerTest)}] RequiredBytes: {Math.Ceiling(requiredBitsOutput[0] / 8.0)}, {Math.Ceiling(requiredBitsOutput[1] / 8.0)}, {Math.Ceiling(requiredBitsOutput[2] / 8.0)}");
                Console.WriteLine("");

                previousPosition = position;
            }

            Console.WriteLine("----------------------------------------");
        }

        static Quaternion GenerateRandomQuaternion()
        {
            var random = new Random();
            var u1 = random.NextDouble(); // [0, 1]
            var u2 = random.NextDouble(); // [0, 1]
            var u3 = random.NextDouble(); // [0, 1]

            var x = (float)(Math.Sqrt(1 - u1) * Math.Sin(2.0f * Math.PI * u2));
            var y = (float)(Math.Sqrt(1 - u1) * Math.Cos(2.0f * Math.PI * u2));
            var z = (float)(Math.Sqrt(u1) * Math.Sin(2.0f * Math.PI * u3));
            var w = (float)(Math.Sqrt(u1) * Math.Cos(2.0f * Math.PI * u3));

            return new Quaternion(x, y, z, w);
        }

        static float Angle(Quaternion a, Quaternion b)
        {
            var dot = Math.Min(Math.Abs(Dot(a, b)), 1.0f);
            var isEqualUsingDot = dot > 1.0f - Epsilon;
            return isEqualUsingDot ? 0.0f : (float)Math.Acos(dot) * 2.0f * Rad2Deg;
        }

        static float Dot(Quaternion a, Quaternion b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        }
    }
}