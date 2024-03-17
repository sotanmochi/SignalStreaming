using System;
using NetStack.Quantization;

#if !(ENABLE_MONO || ENABLE_IL2CPP)
using System.Numerics;
#else
using UnityEngine;
#endif

namespace SignalStreaming.Quantization
{
    public sealed class VectorQuantizer
    {
        public enum DeltaType : byte
        {
            OutOfDeltaRange = 0,
            XYZ = 1,
            XY = 2,
            XZ = 3,
            YZ = 4,
            X = 5,
            Y = 6,
            Z = 7,
            Zero = 8,
        }

        public static Vector3 DefaultDeltaValues => new Vector3(0.25f, 0.25f, 0.25f);
        public static float DefaultDeltaPrecision => 0.002f;

        readonly BoundedRange[] _boundedRanges;
        readonly BoundedRange[] _deltaBoundedRanges;

        Vector3 _previousValue;
        Vector3 _dequantizedPreviousValue;

        public int[] RequiredBits { get; } = new int[3];
        public int[] DeltaRequiredBits { get; } = new int[3];

        public VectorQuantizer(BoundedRange[] boundedRanges, BoundedRange[] deltaBoundedRanges = null)
        {
            if (boundedRanges == null || boundedRanges.Length != 3)
            {
                throw new System.ArgumentException("BoundedRanges must have 3 elements");
            }

            if (deltaBoundedRanges != null && deltaBoundedRanges.Length != 3)
            {
                throw new System.ArgumentException("DeltaBoundedRanges must have 3 elements");
            }

            _boundedRanges = boundedRanges;
            _deltaBoundedRanges = deltaBoundedRanges;

            if (_deltaBoundedRanges == null)
            {
                _deltaBoundedRanges = new BoundedRange[3];
#if ENABLE_MONO || ENABLE_IL2CPP
                _deltaBoundedRanges[0] = new BoundedRange(-DefaultDeltaValues.x, DefaultDeltaValues.x, DefaultDeltaPrecision);
                _deltaBoundedRanges[1] = new BoundedRange(-DefaultDeltaValues.y, DefaultDeltaValues.y, DefaultDeltaPrecision);
                _deltaBoundedRanges[2] = new BoundedRange(-DefaultDeltaValues.z, DefaultDeltaValues.z, DefaultDeltaPrecision);
#else
                _deltaBoundedRanges[0] = new BoundedRange(-DefaultDeltaValues.X, DefaultDeltaValues.X, DefaultDeltaPrecision);
                _deltaBoundedRanges[1] = new BoundedRange(-DefaultDeltaValues.Y, DefaultDeltaValues.Y, DefaultDeltaPrecision);
                _deltaBoundedRanges[2] = new BoundedRange(-DefaultDeltaValues.Z, DefaultDeltaValues.Z, DefaultDeltaPrecision);
#endif
            }

            RequiredBits[0] = _boundedRanges[0].RequiredBits;
            RequiredBits[1] = _boundedRanges[1].RequiredBits;
            RequiredBits[2] = _boundedRanges[2].RequiredBits;

            DeltaRequiredBits[0] = _deltaBoundedRanges[0].RequiredBits;
            DeltaRequiredBits[1] = _deltaBoundedRanges[1].RequiredBits;
            DeltaRequiredBits[2] = _deltaBoundedRanges[2].RequiredBits;

#if ENABLE_MONO || ENABLE_IL2CPP
            _previousValue = Vector3.zero;
            _dequantizedPreviousValue = Vector3.zero;
#else
            _previousValue = Vector3.Zero;
            _dequantizedPreviousValue = Vector3.Zero;
#endif
        }

        public void Quantize(Vector3 value, out QuantizedVector3 quantizedValue, out DeltaType deltaType, int[] requiredBitsOutput)
        {
            if (requiredBitsOutput == null || requiredBitsOutput.Length != 3)
            {
                throw new System.ArgumentException("RequiredBitsOutput must have 3 elements");
            }

            var precisionX = _boundedRanges[0].Precision;
            var precisionY = _boundedRanges[1].Precision;
            var precisionZ = _boundedRanges[2].Precision;

            var deltaPrecisionX = _deltaBoundedRanges[0].Precision;
            var deltaPrecisionY = _deltaBoundedRanges[1].Precision;
            var deltaPrecisionZ = _deltaBoundedRanges[2].Precision;

            var deltaMinX = _deltaBoundedRanges[0].MinValue;
            var deltaMinY = _deltaBoundedRanges[1].MinValue;
            var deltaMinZ = _deltaBoundedRanges[2].MinValue;

            var deltaMaxX = _deltaBoundedRanges[0].MaxValue;
            var deltaMaxY = _deltaBoundedRanges[1].MaxValue;
            var deltaMaxZ = _deltaBoundedRanges[2].MaxValue;

#if ENABLE_MONO || ENABLE_IL2CPP
            var x = value.x;
            var y = value.y;
            var z = value.z;

            var dx = x - _previousValue.x;
            var dy = y - _previousValue.y;
            var dz = z - _previousValue.z;
#else
            var x = value.X;
            var y = value.Y;
            var z = value.Z;

            var dx = x - _previousValue.X;
            var dy = y - _previousValue.Y;
            var dz = z - _previousValue.Z;
#endif

            var absDx = Math.Abs(dx);
            var absDy = Math.Abs(dy);
            var absDz = Math.Abs(dz);

            if (absDx <= deltaPrecisionX && absDy <= deltaPrecisionY && absDz <= deltaPrecisionZ)
            {
                deltaType = DeltaType.Zero;

                quantizedValue = new QuantizedVector3(0, 0, 0);

                requiredBitsOutput[0] = 0;
                requiredBitsOutput[1] = 0;
                requiredBitsOutput[2] = 0;
            }
            else if (deltaMinX <= dx && dx <= deltaMaxX && deltaMinY <= dy && dy <= deltaMaxY && deltaMinZ <= dz && dz <= deltaMaxZ)
            {
                deltaType = DeltaType.XYZ;

                var qdx = _deltaBoundedRanges[0].Quantize(dx);
                var qdy = _deltaBoundedRanges[1].Quantize(dy);
                var qdz = _deltaBoundedRanges[2].Quantize(dz);
                quantizedValue = new QuantizedVector3(qdx, qdy, qdz);

                requiredBitsOutput[0] = _deltaBoundedRanges[0].RequiredBits;
                requiredBitsOutput[1] = _deltaBoundedRanges[1].RequiredBits;
                requiredBitsOutput[2] = _deltaBoundedRanges[2].RequiredBits;
            }
            else if (deltaMinX <= dx && dx <= deltaMaxX && deltaMinY <= dy && dy <= deltaMaxY)
            {
                deltaType = DeltaType.XY;

                var qdx = _deltaBoundedRanges[0].Quantize(dx);
                var qdy = _deltaBoundedRanges[1].Quantize(dy);
                var qz = _boundedRanges[2].Quantize(z);
                quantizedValue = new QuantizedVector3(qdx, qdy, qz);

                requiredBitsOutput[0] = _deltaBoundedRanges[0].RequiredBits;
                requiredBitsOutput[1] = _deltaBoundedRanges[1].RequiredBits;
                requiredBitsOutput[2] = _boundedRanges[2].RequiredBits;
            }
            else if (deltaMinX <= dx && dx <= deltaMaxX && deltaMinZ <= dz && dz <= deltaMaxZ)
            {
                deltaType = DeltaType.XZ;

                var qdx = _deltaBoundedRanges[0].Quantize(dx);
                var qy = _boundedRanges[1].Quantize(y);
                var qdz = _deltaBoundedRanges[2].Quantize(dz);
                quantizedValue = new QuantizedVector3(qdx, qy, qdz);

                requiredBitsOutput[0] = _deltaBoundedRanges[0].RequiredBits;
                requiredBitsOutput[1] = _boundedRanges[1].RequiredBits;
                requiredBitsOutput[2] = _deltaBoundedRanges[2].RequiredBits;
            }
            else if (deltaMinY <= dy && dy <= deltaMaxY && deltaMinZ <= dz && dz <= deltaMaxZ)
            {
                deltaType = DeltaType.YZ;

                var qx = _boundedRanges[0].Quantize(x);
                var qdy = _deltaBoundedRanges[1].Quantize(dy);
                var qdz = _deltaBoundedRanges[2].Quantize(dz);
                quantizedValue = new QuantizedVector3(qx, qdy, qdz);

                requiredBitsOutput[0] = _boundedRanges[0].RequiredBits;
                requiredBitsOutput[1] = _deltaBoundedRanges[1].RequiredBits;
                requiredBitsOutput[2] = _deltaBoundedRanges[2].RequiredBits;
            }
            else if (deltaMinX <= dx && dx <= deltaMaxX)
            {
                deltaType = DeltaType.X;

                var qdx = _deltaBoundedRanges[0].Quantize(dx);
                var qy = _boundedRanges[1].Quantize(y);
                var qz = _boundedRanges[2].Quantize(z);
                quantizedValue = new QuantizedVector3(qdx, qy, qz);

                requiredBitsOutput[0] = _deltaBoundedRanges[0].RequiredBits;
                requiredBitsOutput[1] = _boundedRanges[1].RequiredBits;
                requiredBitsOutput[2] = _boundedRanges[2].RequiredBits;
            }
            else if (deltaMinY <= dy && dy <= deltaMaxY)
            {
                deltaType = DeltaType.Y;

                var qx = _boundedRanges[0].Quantize(x);
                var qdy = _deltaBoundedRanges[1].Quantize(dy);
                var qz = _boundedRanges[2].Quantize(z);
                quantizedValue = new QuantizedVector3(qx, qdy, qz);

                requiredBitsOutput[0] = _boundedRanges[0].RequiredBits;
                requiredBitsOutput[1] = _deltaBoundedRanges[1].RequiredBits;
                requiredBitsOutput[2] = _boundedRanges[2].RequiredBits;
            }
            else if (deltaMinZ <= dz && dz <= deltaMaxZ)
            {
                deltaType = DeltaType.Z;

                var qx = _boundedRanges[0].Quantize(x);
                var qy = _boundedRanges[1].Quantize(y);
                var qdz = _deltaBoundedRanges[2].Quantize(dz);
                quantizedValue = new QuantizedVector3(qx, qy, qdz);

                requiredBitsOutput[0] = _boundedRanges[0].RequiredBits;
                requiredBitsOutput[1] = _boundedRanges[1].RequiredBits;
                requiredBitsOutput[2] = _deltaBoundedRanges[2].RequiredBits;
            }
            else
            {
                deltaType = DeltaType.OutOfDeltaRange;
                quantizedValue = BoundedRange.Quantize(value, _boundedRanges);
                requiredBitsOutput[0] = _boundedRanges[0].RequiredBits;
                requiredBitsOutput[1] = _boundedRanges[1].RequiredBits;
                requiredBitsOutput[2] = _boundedRanges[2].RequiredBits;
                _previousValue = value;
            }
        }

        public void Dequantize(uint[] quantizedVector3, DeltaType deltaType, out Vector3 dequantizedValue)
        {
            if (quantizedVector3 == null || quantizedVector3.Length != 3)
            {
                throw new System.ArgumentException("QuantizedVector3 must have 3 elements");
            }

            if (deltaType == DeltaType.OutOfDeltaRange)
            {
                var x = _boundedRanges[0].Dequantize(quantizedVector3[0]);
                var y = _boundedRanges[1].Dequantize(quantizedVector3[1]);
                var z = _boundedRanges[2].Dequantize(quantizedVector3[2]);
                dequantizedValue = new Vector3(x, y, z);
                _dequantizedPreviousValue = dequantizedValue;
            }
            else if (deltaType == DeltaType.Zero)
            {
                dequantizedValue = _dequantizedPreviousValue;
            }
            else
            {
                var dx = _deltaBoundedRanges[0].Dequantize(quantizedVector3[0]);
                var dy = _deltaBoundedRanges[1].Dequantize(quantizedVector3[1]);
                var dz = _deltaBoundedRanges[2].Dequantize(quantizedVector3[2]);

                var x = _boundedRanges[0].Dequantize(quantizedVector3[0]);
                var y = _boundedRanges[1].Dequantize(quantizedVector3[1]);
                var z = _boundedRanges[2].Dequantize(quantizedVector3[2]);

#if ENABLE_MONO || ENABLE_IL2CPP
                var prevX = _dequantizedPreviousValue.x;
                var prevY = _dequantizedPreviousValue.y;
                var prevZ = _dequantizedPreviousValue.z;
#else
                var prevX = _dequantizedPreviousValue.X;
                var prevY = _dequantizedPreviousValue.Y;
                var prevZ = _dequantizedPreviousValue.Z;
#endif

                if (deltaType == DeltaType.XYZ)
                {
                    dequantizedValue = new Vector3(prevX + dx, prevY + dy, prevZ + dz);
                }
                else if (deltaType == DeltaType.XY)
                {
                    dequantizedValue = new Vector3(prevX + dx, prevY + dy, z);
                }
                else if (deltaType == DeltaType.XZ)
                {
                    dequantizedValue = new Vector3(prevX + dx, y, prevZ + dz);
                }
                else if (deltaType == DeltaType.YZ)
                {
                    dequantizedValue = new Vector3(x, prevY + dy, prevZ + dz);
                }
                else if (deltaType == DeltaType.X)
                {
                    dequantizedValue = new Vector3(prevX + dx, y, z);
                }
                else if (deltaType == DeltaType.Y)
                {
                    dequantizedValue = new Vector3(x, prevY + dy, z);
                }
                else if (deltaType == DeltaType.Z)
                {
                    dequantizedValue = new Vector3(x, y, prevZ + dz);
                }
                else
                {
                    throw new System.ArgumentException("Invalid DeltaType");
                }
            }
        }
    }
}
