/*
 *  The original source code is available on GitHub.
 *  https://github.com/nxrighthere/NetStack/blob/master/Source/NetStack.Quantization/SmallestThree.cs
 *
 *  ---
 *
 *  Copyright (c) 2020 Stanislav Denisov, Maxim Munning, Davin Carten
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
    public static class SmallestThree
    {
        private const float smallestThreeUnpack = 0.70710678118654752440084436210485f + 0.0000001f;
        private const float smallestThreePack = 1.0f / smallestThreeUnpack;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static QuantizedQuaternion Quantize(System.Numerics.Quaternion quaternion, int bitsPerElement = 12)
        {
            return Quantize(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W, bitsPerElement);
        }

#if UNITY_ENGINE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static QuantizedQuaternion Quantize(UnityEngine.Quaternion quaternion, int bitsPerElement = 12)
        {
            return Quantize(quaternion.x, quaternion.y, quaternion.z, quaternion.w, bitsPerElement);
        }
#endif

        public static QuantizedQuaternion Quantize(float qx, float qy, float qz, float qw, int bitsPerElement = 12)
        {
            float halfRange = (1 << bitsPerElement - 1);
            float packer = smallestThreePack * halfRange;
            float maxValue = float.MinValue;
            bool signMinus = false;
            uint m = 0;
            uint a = 0;
            uint b = 0;
            uint c = 0;

            for (uint i = 0; i <= 3; i++)
            {
                float element = 0.0f;
                float abs = 0.0f;

                switch (i)
                {
                    case 0:
                        element = qx;

                        break;

                    case 1:
                        element = qy;

                        break;

                    case 2:
                        element = qz;

                        break;

                    case 3:
                        element = qw;

                        break;
                }

                abs = Math.Abs(element);

                if (abs > maxValue)
                {
                    signMinus = (element < 0.0f);
                    m = i;
                    maxValue = abs;
                }
            }

            float af = 0.0f;
            float bf = 0.0f;
            float cf = 0.0f;

            switch (m)
            {
                case 0:
                    af = qy;
                    bf = qz;
                    cf = qw;

                    break;
                case 1:
                    af = qx;
                    bf = qz;
                    cf = qw;

                    break;
                case 2:
                    af = qx;
                    bf = qy;
                    cf = qw;

                    break;
                default:
                    af = qx;
                    bf = qy;
                    cf = qz;

                    break;
            }

            if (signMinus)
            {
                a = (uint)((-af * packer) + halfRange);
                b = (uint)((-bf * packer) + halfRange);
                c = (uint)((-cf * packer) + halfRange);
            }
            else
            {
                a = (uint)((af * packer) + halfRange);
                b = (uint)((bf * packer) + halfRange);
                c = (uint)((cf * packer) + halfRange);
            }

            return new QuantizedQuaternion(m, a, b, c);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Unpack(QuantizedQuaternion data, int bitsPerElement, out float a, out float b, out float c, out float d)
        {
            int halfRange = (1 << bitsPerElement - 1);
            float unpacker = smallestThreeUnpack * (1.0f / halfRange);

            int ai = (int)data.a;
            int bi = (int)data.b;
            int ci = (int)data.c;

            ai -= halfRange;
            bi -= halfRange;
            ci -= halfRange;

            a = ai * unpacker;
            b = bi * unpacker;
            c = ci * unpacker;

            d = (float)Math.Sqrt(1.0f - ((a * a) + (b * b) + (c * c)));
        }

        public static void DequantizeTo(ref System.Numerics.Quaternion output, QuantizedQuaternion data, int bitsPerElement = 12)
        {
            Unpack(data, bitsPerElement, out float a, out float b, out float c, out float d);

            switch (data.m)
            {
                case 0:
                    output.X = d;
                    output.Y = a;
                    output.Z = b;
                    output.W = c;
                    break;

                case 1:
                    output.X = a;
                    output.Y = d;
                    output.Z = b;
                    output.W = c;
                    break;

                case 2:
                    output.X = a;
                    output.Y = b;
                    output.Z = d;
                    output.W = c;
                    break;

                default:
                    output.X = a;
                    output.Y = b;
                    output.Z = c;
                    output.W = d;
                    break;
            }
        }

#if UNITY_ENGINE
        public static void DequantizeTo(ref UnityEngine.Quaternion output, QuantizedQuaternion data, int bitsPerElement = 12)
        {
            Unpack(data, bitsPerElement, out float a, out float b, out float c, out float d);

            switch (data.m)
            {
                case 0:
                    output.x = d;
                    output.y = a;
                    output.z = b;
                    output.w = c;
                    break;

                case 1:
                    output.x = a;
                    output.y = d;
                    output.z = b;
                    output.w = c;
                    break;

                case 2:
                    output.x = a;
                    output.y = b;
                    output.z = d;
                    output.w = c;
                    break;

                default:
                    output.x = a;
                    output.y = b;
                    output.z = c;
                    output.w = d;
                    break;
            }
        }
#endif
    }
}