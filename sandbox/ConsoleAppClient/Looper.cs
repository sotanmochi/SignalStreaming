using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SignalStreamingSamples.ConsoleAppClient
{
    public interface IStartable { void Start(); }
    public interface ITickable { void Tick(); }

    public class Looper : IDisposable
    {
        readonly struct LoopItem
        {
            public readonly object Target;
            public readonly Action Action;

            public LoopItem(object target, Action action)
            {
                Target = target;
                Action = action;
            }
        }

        static readonly double TimestampsToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        readonly int _targetFrameTimeMilliseconds;
        readonly List<LoopItem> _startableItems = new();
        readonly List<LoopItem> _tickableItems = new();
        readonly List<LoopItem> _disposableItems = new();

        public uint FrameCount { get; private set; }

        public Looper(int targetFrameRate)
        {
            _targetFrameTimeMilliseconds = (int)(1000 / (double)targetFrameRate);
        }

        public void Dispose()
        {
            _startableItems.Clear();
            _tickableItems.Clear();

            for (var i = 0; i < _disposableItems.Count; i++)
            {
                _disposableItems[i].Action();
            }
            _disposableItems.Clear();
        }

        public void Register(object target)
        {
            if (target is IDisposable disposable)
            {
                var item = new LoopItem(target, disposable.Dispose);
                if (!_disposableItems.Contains(item))
                {
                    _disposableItems.Add(item);
                }
            }
            if (target is IStartable startable)
            {
                var item = new LoopItem(target, startable.Start);
                if (!_startableItems.Contains(item))
                {
                    _startableItems.Add(item);
                }
            }
            if (target is ITickable tickable)
            {
                var item = new LoopItem(target, tickable.Tick);
                if (!_tickableItems.Contains(item))
                {
                    _tickableItems.Add(item);
                }
            }
        }

        public void StartLoop(CancellationToken cancellationToken)
        {
            Log($"[{nameof(Looper)}] StartLoop (Thread: {Thread.CurrentThread.ManagedThreadId})");

            OnStart();

            while (!cancellationToken.IsCancellationRequested)
            {
                var begin = Stopwatch.GetTimestamp();

                FrameCount++;
                OnTick();

                var end = Stopwatch.GetTimestamp();
                var elapsed = (end - begin) * TimestampsToTicks;
                var sleepTime = _targetFrameTimeMilliseconds - (int)(elapsed * 1000);
                if (sleepTime > 0)
                {
                    Thread.Sleep(sleepTime);
                }
            }
        }

        void OnStart()
        {
            for (var i = 0; i < _startableItems.Count; i++)
            {
                _startableItems[i].Action();
            }
        }

        void OnTick()
        {
            for (var i = 0; i < _tickableItems.Count; i++)
            {
                _tickableItems[i].Action();
            }
        }

        void Log(object message)
        {
            Console.WriteLine(message);
        }
    }
}
