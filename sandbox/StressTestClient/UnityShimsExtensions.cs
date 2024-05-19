using System;
using UnityEngine; // MessagePack.UnityShims

namespace Sandbox.StressTest.Client
{
    public static class UnityShimsExtensions
    {
        public const float Degrees2Radian = MathF.PI / 180.0f;

        /// <summary>
        /// Create a new Quaternion from Euler angles.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns>The created Quaternion.</returns>
        public static Quaternion CreateQuaternion(float x, float y, float z)
        {
            float rx = x * Degrees2Radian;
            float ry = y * Degrees2Radian;
            float rz = z * Degrees2Radian;

            float sx = MathF.Sin(rx * 0.5f);
            float cx = MathF.Cos(rx * 0.5f);
            float sy = MathF.Sin(ry * 0.5f);
            float cy = MathF.Cos(ry * 0.5f);
            float sz = MathF.Sin(rz * 0.5f);
            float cz = MathF.Cos(rz * 0.5f);

            return new Quaternion(
                cy * sx * cz + sy * cx * sz,
                sy * cx * cz - cy * sx * sz,
                cy * cx * sz - sy * sx * cz,
                cy * cx * cz + sy * sx * sz
            );
        }

        /// <summary>
        /// Rotates the point by the rotation.
        /// </summary>
        /// <param name="rotation"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static Vector3 Rotate(this Quaternion rotation, Vector3 point)
        {
            float x = rotation.x * 2f;
            float y = rotation.y * 2f;
            float z = rotation.z * 2f;
            float xx = rotation.x * x;
            float yy = rotation.y * y;
            float zz = rotation.z * z;
            float xy = rotation.x * y;
            float xz = rotation.x * z;
            float yz = rotation.y * z;
            float wx = rotation.w * x;
            float wy = rotation.w * y;
            float wz = rotation.w * z;

            return new Vector3(
                (1f - (yy + zz)) * point.x + (xy - wz) * point.y + (xz + wy) * point.z,
                (xy + wz) * point.x + (1f - (xx + zz)) * point.y + (yz - wx) * point.z,
                (xz - wy) * point.x + (yz + wx) * point.y + (1f - (xx + yy)) * point.z
            );
        }
    }
}