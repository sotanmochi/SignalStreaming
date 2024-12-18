/*
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

namespace SignalStreaming.Quantization
{
    public struct QuantizedVector2
    {
        public uint x;
        public uint y;

        public QuantizedVector2(uint x, uint y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public struct QuantizedVector3
    {
        public uint x;
        public uint y;
        public uint z;

        public QuantizedVector3(uint x, uint y, uint z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    public struct QuantizedVector4
    {
        public uint x;
        public uint y;
        public uint z;
        public uint w;

        public QuantizedVector4(uint x, uint y, uint z, uint w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
    }

    public sealed class QuantizedVector
    {
        byte _requiredBitsPerElement;

        public uint[] Elements { get; }

        public byte Size => (byte)Elements.Length;

        public byte RequiredBitsPerElement
        {
            get => _requiredBitsPerElement;
            set => _requiredBitsPerElement = System.Math.Clamp(value, (byte)1, (byte)32);
        }

        public QuantizedVector(byte size, byte requiredBitsPerElement)
        {
            Elements = new uint[size];
            RequiredBitsPerElement = requiredBitsPerElement;
        }
    }
}