using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Sandbox
{
    public sealed class PerformanceSpikeSimulator
    {
        public enum SpikeIntervalType
        {
            FixedInterval,
            RandomInterval,
        }

        static readonly double TimestampsToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        readonly Random _random = new();

        float _spikeFrameDeltaTimeMs;
        float _lastSpikeBeginTimestamp;

        public bool IsRunning { get; private set; }
        public SpikeIntervalType IntervalType { get; private set; }

        public float FixedSpikeIntervalMs { get; private set; }
        public float RandomSpikeMaxIntervalMs { get; private set; }
        public float RandomSpikeNextIntervalMs { get; private set; }

        public float SpikeFrameDeltaTimeMs
        {
            get => _spikeFrameDeltaTimeMs;
            set
            {
                if (IntervalType == SpikeIntervalType.FixedInterval)
                {
                    if (value < 0.5f * FixedSpikeIntervalMs)
                    {
                        _spikeFrameDeltaTimeMs = value;
                    }
                    else
                    {
                        _spikeFrameDeltaTimeMs = 0.5f * FixedSpikeIntervalMs;
                    }
                }
                else
                {
                    _spikeFrameDeltaTimeMs = value;
                }
            }
        }

        public void StartFixedIntervalSpikes(float intervalTimeMilliseconds, float spikeFrameDeltaTimeMilliseconds = 100f)
        {
            IntervalType = SpikeIntervalType.FixedInterval;

            FixedSpikeIntervalMs = intervalTimeMilliseconds;
            SpikeFrameDeltaTimeMs = spikeFrameDeltaTimeMilliseconds;

            _lastSpikeBeginTimestamp = Stopwatch.GetTimestamp();
            IsRunning = true;
        }

        public void StartRandomIntervalSpikes(float maxIntervalTimeMilliseconds, float spikeFrameDeltaTimeMilliseconds = 100f)
        {
            IntervalType = SpikeIntervalType.RandomInterval;

            RandomSpikeMaxIntervalMs = maxIntervalTimeMilliseconds;
            SpikeFrameDeltaTimeMs = spikeFrameDeltaTimeMilliseconds;

            _lastSpikeBeginTimestamp = Stopwatch.GetTimestamp();
            RandomSpikeNextIntervalMs = GetRandomSpikeIntervalMilliseconds();
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void Tick()
        {
            if (!IsRunning) return;

            var beginTimestamp = Stopwatch.GetTimestamp();

            var elapsedTicks = (beginTimestamp - _lastSpikeBeginTimestamp) * TimestampsToTicks;
            var elapsedMilliseconds = (long)elapsedTicks / TimeSpan.TicksPerMillisecond;

            if (IntervalType == SpikeIntervalType.FixedInterval && elapsedMilliseconds >= FixedSpikeIntervalMs)
            {
                _lastSpikeBeginTimestamp = beginTimestamp;
                Thread.Sleep((int)SpikeFrameDeltaTimeMs);
            }
            else if (IntervalType == SpikeIntervalType.RandomInterval && elapsedMilliseconds >= RandomSpikeNextIntervalMs)
            {
                _lastSpikeBeginTimestamp = beginTimestamp;
                Thread.Sleep((int)SpikeFrameDeltaTimeMs);
                RandomSpikeNextIntervalMs = GetRandomSpikeIntervalMilliseconds();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float GetRandomSpikeIntervalMilliseconds()
        {
            var min = 2.0f * SpikeFrameDeltaTimeMs;
            var max = RandomSpikeMaxIntervalMs;
            return (float)(_random.NextDouble() * (max - min) + min);
        }
    }
}