using System;
using System.Runtime.InteropServices;

namespace SignalStreaming.SerializationTest
{
    [StructLayout(LayoutKind.Explicit)]
    struct UnionValue
    {
        [FieldOffset(0)]
        float f;
        [FieldOffset(0)]
        int i;
        [FieldOffset(0)]
        uint u;

        public UnionValue(float f) => this.f = f;
        public UnionValue(int i) => this.i = i;
        public UnionValue(uint u) => this.u = u;

        public float AsFloat() => f;
        public int AsInt() => i;
        public uint AsUInt() => u;

        public static implicit operator UnionValue(float f)
        {
            return new UnionValue(f);
        }

        public static implicit operator UnionValue(int i)
        {
            return new UnionValue(i);
        }

        public static implicit operator UnionValue(uint u)
        {
            return new UnionValue(u);
        }
    }
}
