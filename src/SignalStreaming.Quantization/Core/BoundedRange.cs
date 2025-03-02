﻿/*
 *  The original source code is available on GitHub.
 *  https://github.com/nxrighthere/NetStack/blob/master/Source/NetStack.Quantization/BoundedRange.cs
 *
 *  ---
 *
 *  Copyright (c) 2024 Soichiro Sugimoto
 *  Copyright (c) 2018 Stanislav Denisov
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

#if ENABLE_MONO || ENABLE_IL2CPP
#define UNITY_ENGINE
#endif

using System;
using System.Runtime.CompilerServices;

namespace SignalStreaming.Quantization
{
    public class BoundedRange
    {
        private readonly float minValue;
        private readonly float maxValue;

        private float precision;
        private int requiredBits;
        private uint mask;

        public float Precision
        {
            get { return precision; }
            set
            {
                if (value <= 0f) throw new ArgumentOutOfRangeException("Precision must be greater than zero.");
                precision = value;
                requiredBits = CalculateRequiredBits(minValue, maxValue, precision);
                mask = (uint)((1L << requiredBits) - 1);
            }
        }

        public int RequiredBits => requiredBits;

        public BoundedRange(float minValue, float maxValue, float precision)
        {
            this.minValue = minValue;
            this.maxValue = maxValue;
            this.precision = precision;

            requiredBits = CalculateRequiredBits(minValue, maxValue, precision);
            mask = (uint)((1L << requiredBits) - 1);
        }

        private int CalculateRequiredBits(float minValue, float maxValue, float precision)
        {
            return Log2((uint)((maxValue - minValue) * (1.0f / precision) + 0.5f)) + 1;
        }

        private int Log2(uint value)
        {
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;

            return DeBruijn.Lookup[(value * 0x07C4ACDDU) >> 27];
        }

        [MethodImpl(256)]
        public uint Quantize(float value)
        {
            if (value < minValue)
                value = minValue;
            else if (value > maxValue)
                value = maxValue;

            return (uint)((float)((value - minValue) * (1f / precision)) + 0.5f) & mask;
        }

        [MethodImpl(256)]
        public float Dequantize(uint data)
        {
            float adjusted = ((float)data * precision) + minValue;

            if (adjusted < minValue)
                adjusted = minValue;
            else if (adjusted > maxValue)
                adjusted = maxValue;

            return adjusted;
        }

        public void Quantize(float[] vector, QuantizedVector output)
        {
            for (int i = 0; i < output.Elements.Length; i++)
            {
                output.Elements[i] = Quantize(vector[i]);
            }
        }

        public void Dequantize(QuantizedVector data, float[] output)
        {
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = Dequantize(data.Elements[i]);
            }
        }

#if UNITY_ENGINE
        public static QuantizedVector2 Quantize(UnityEngine.Vector2 vector2, BoundedRange[] boundedRange)
        {
            QuantizedVector2 data = default(QuantizedVector2);

            data.x = boundedRange[0].Quantize(vector2.x);
            data.y = boundedRange[1].Quantize(vector2.y);

            return data;
        }
#endif

        public static QuantizedVector2 Quantize(System.Numerics.Vector2 vector2, BoundedRange[] boundedRange)
        {
            QuantizedVector2 data = default(QuantizedVector2);

            data.x = boundedRange[0].Quantize(vector2.X);
            data.y = boundedRange[1].Quantize(vector2.Y);

            return data;
        }

#if UNITY_ENGINE
        public static QuantizedVector3 Quantize(UnityEngine.Vector3 vector3, BoundedRange[] boundedRange)
        {
            QuantizedVector3 data = default(QuantizedVector3);

            data.x = boundedRange[0].Quantize(vector3.x);
            data.y = boundedRange[1].Quantize(vector3.y);
            data.z = boundedRange[2].Quantize(vector3.z);

            return data;
        }
#endif

        public static QuantizedVector3 Quantize(System.Numerics.Vector3 vector3, BoundedRange[] boundedRange)
        {
            QuantizedVector3 data = default(QuantizedVector3);

            data.x = boundedRange[0].Quantize(vector3.X);
            data.y = boundedRange[1].Quantize(vector3.Y);
            data.z = boundedRange[2].Quantize(vector3.Z);

            return data;
        }

#if UNITY_ENGINE
        public static QuantizedVector4 Quantize(UnityEngine.Vector4 vector4, BoundedRange[] boundedRange)
        {
            QuantizedVector4 data = default(QuantizedVector4);

            data.x = boundedRange[0].Quantize(vector4.x);
            data.y = boundedRange[1].Quantize(vector4.y);
            data.z = boundedRange[2].Quantize(vector4.z);
            data.w = boundedRange[3].Quantize(vector4.w);

            return data;
        }
#endif

        public static QuantizedVector4 Quantize(System.Numerics.Vector4 vector4, BoundedRange[] boundedRange)
        {
            QuantizedVector4 data = default(QuantizedVector4);

            data.x = boundedRange[0].Quantize(vector4.X);
            data.y = boundedRange[1].Quantize(vector4.Y);
            data.z = boundedRange[2].Quantize(vector4.Z);
            data.w = boundedRange[3].Quantize(vector4.W);

            return data;
        }

        public static void DequantizeTo(ref System.Numerics.Vector2 output, QuantizedVector2 data, BoundedRange[] boundedRange)
        {
            output.X = boundedRange[0].Dequantize(data.x);
            output.Y = boundedRange[1].Dequantize(data.y);
        }

#if UNITY_ENGINE
        public static void DequantizeTo(ref UnityEngine.Vector2 output, QuantizedVector2 data, BoundedRange[] boundedRange)
        {
            output.x = boundedRange[0].Dequantize(data.x);
            output.y = boundedRange[1].Dequantize(data.y);
        }
#endif

        public static void DequantizeTo(ref System.Numerics.Vector3 output, QuantizedVector3 data, BoundedRange[] boundedRange)
        {
            output.X = boundedRange[0].Dequantize(data.x);
            output.Y = boundedRange[1].Dequantize(data.y);
            output.Z = boundedRange[2].Dequantize(data.z);
        }

#if UNITY_ENGINE
        public static void DequantizeTo(ref UnityEngine.Vector3 output, QuantizedVector3 data, BoundedRange[] boundedRange)
        {
            output.x = boundedRange[0].Dequantize(data.x);
            output.y = boundedRange[1].Dequantize(data.y);
            output.z = boundedRange[2].Dequantize(data.z);
        }
#endif

        public static void DequantizeTo(ref System.Numerics.Vector4 output, QuantizedVector4 data, BoundedRange[] boundedRange)
        {
            output.X = boundedRange[0].Dequantize(data.x);
            output.Y = boundedRange[1].Dequantize(data.y);
            output.Z = boundedRange[2].Dequantize(data.z);
            output.W = boundedRange[3].Dequantize(data.w);
        }

#if UNITY_ENGINE
        public static void DequantizeTo(ref UnityEngine.Vector4 output, QuantizedVector4 data, BoundedRange[] boundedRange)
        {
            output.x = boundedRange[0].Dequantize(data.x);
            output.y = boundedRange[1].Dequantize(data.y);
            output.z = boundedRange[2].Dequantize(data.z);
            output.w = boundedRange[3].Dequantize(data.w);
        }
#endif
    }

    public static class DeBruijn
    {
        public static readonly int[] Lookup = new int[32]
        {
            0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
            8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
        };
    }
}