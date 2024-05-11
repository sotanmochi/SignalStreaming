using System;
using System.Runtime.InteropServices;
using MessagePack;
using MessagePack.Resolvers;
using SignalStreaming.Quantization;
using SignalStreaming.Serialization;
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
            System.Numerics.Quaternion dequantizedQ = default;
            SmallestThree.DequantizeTo(ref dequantizedQ, deserializedQq);
            var dequantizedUnityQuaternion = dequantizedQ.ToUnityQuaternion();
            var angle = Angle(q, dequantizedUnityQuaternion);

            Console.WriteLine($"[{nameof(QuantizedQuaternionTest)}] q: ({q.x}, {q.y}, {q.z}, {q.w})");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest)}] dequantizedQ: ({dequantizedQ.X}, {dequantizedQ.Y}, {dequantizedQ.Z}, {dequantizedQ.W})");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest)}] Dot(q, dequantizedQ): {Dot(q, dequantizedUnityQuaternion)}");
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
            System.Numerics.Quaternion dequantizedQ = default;
            SmallestThree.DequantizeTo(ref dequantizedQ, deserializedQq);
            var dequantizedUnityQuaternion = dequantizedQ.ToUnityQuaternion();
            var angle = Angle(q, dequantizedUnityQuaternion);

            Console.WriteLine($"[{nameof(QuantizedQuaternionTest2)}] q: ({q.x}, {q.y}, {q.z}, {q.w})");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest2)}] dequantizedQ: ({dequantizedQ.X}, {dequantizedQ.Y}, {dequantizedQ.Z}, {dequantizedQ.W})");
            Console.WriteLine($"[{nameof(QuantizedQuaternionTest2)}] Dot(q, dequantizedQ): {Dot(q, dequantizedUnityQuaternion)}");
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
            System.Numerics.Vector3 dequantizedVector = default;
            BoundedRange.DequantizeTo(ref dequantizedVector, deserializedQv, worldBounds);
            var dequantizedPosition = dequantizedVector.ToUnityVector3();

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
            System.Numerics.Vector3 dequantizedVector = default;
            BoundedRange.DequantizeTo(ref dequantizedVector, deserializedQv, worldBounds);
            var dequantizedPosition = dequantizedVector.ToUnityVector3();

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