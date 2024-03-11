namespace SignalStreaming.SerializationTest
{
    public static class SystemNumericsExtensions
    {
        public static UnityEngine.Quaternion ToUnityQuaternion(this System.Numerics.Quaternion quaternion)
        {
            return new UnityEngine.Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        }

        public static System.Numerics.Quaternion ToSystemNumericsQuaternion(this UnityEngine.Quaternion quaternion)
        {
            return new System.Numerics.Quaternion(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
        }
    }
}