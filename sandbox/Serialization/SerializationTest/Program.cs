using System;
using System.Runtime.InteropServices;
using MessagePack;
using MessagePack.Resolvers;
using NetStack.Quantization;
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
            QuantizedQuaternionTest();
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