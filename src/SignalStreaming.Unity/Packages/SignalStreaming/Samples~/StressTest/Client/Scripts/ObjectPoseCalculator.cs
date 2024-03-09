using System;
using System.Diagnostics;
using UnityEngine;

namespace SignalStreaming.Samples.StressTest
{
    public class ObjectPoseCalculator
    {
        readonly Stopwatch stopwatch = new Stopwatch();

        float a = 10.0f;
        float b = 10.0f;
        Vector3 offsetPosition;
        float offsetAngle;

        float previousTime;
        float t;

        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public Vector3 ForwardDirection { get; private set; }

        public void Startup()
        {
            var random = new System.Random();
            stopwatch.Start();
            a = random.Next(5, 101);
            b = random.Next(5, 101);
            offsetPosition = (a > b) ? new Vector3(a / 2f, 0, 0) : new Vector3(0, 0, b / 2f);
            offsetAngle = 360 * (float)random.NextDouble();
        }

        public void Tick()
        {
            var currentTimeMilliseconds = stopwatch.ElapsedMilliseconds;
            var deltaTime = (currentTimeMilliseconds - previousTime) / 1000.0f;
            previousTime = currentTimeMilliseconds;

            var x = offsetPosition.x + a * Mathf.Sin(t);
            var y = offsetPosition.y;
            var z = offsetPosition.z + b * Mathf.Cos(t);

            var vx = a * Mathf.Cos(t);
            var vy = 0;
            var vz = -b * Mathf.Sin(t);

            Position = Quaternion.Euler(0, offsetAngle, 0) * new Vector3(x, y, z);
            Rotation = Quaternion.Euler(0, 360 * t, 0);
            ForwardDirection = Quaternion.Euler(0, offsetAngle, 0) * new Vector3(vx, vy, vz);
            ForwardDirection.Normalize();

            t += deltaTime;
        }
    }
}
