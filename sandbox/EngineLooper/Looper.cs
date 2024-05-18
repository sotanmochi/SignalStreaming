using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sandbox.EngineLooper
{
    public sealed class Looper : IDisposable
    {        
        static readonly double TimestampsToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        readonly ILogger<Looper> _logger;
        readonly Thread _loopThread;
        readonly CancellationTokenSource _loopCancellationTokenSource;
        readonly int _targetFrameTimeMilliseconds;

        List<IFrameTimingObserver> _frameTimingObservers;
        List<LooperAction> _startableActions;
        List<LooperAction> _tickableActions;
        List<LooperAction> _disposableActions;

        ulong _frameCount = 0;
        bool _shutdownRequested;

        public Looper(IOptions<LooperOptions> options, ILogger<Looper> logger) : this(options.Value, logger)
        {
        }

        public Looper(LooperOptions options, ILogger<Looper> logger = null)
        {
            _logger = logger;

            _targetFrameTimeMilliseconds = (int)(1000 / (double)options.TargetFrameRate);

            _frameTimingObservers = new(options.InitialActionsCapacity);
            _startableActions = new(options.InitialActionsCapacity);
            _tickableActions = new(options.InitialActionsCapacity);
            _disposableActions = new(options.InitialActionsCapacity);

            _loopCancellationTokenSource = new CancellationTokenSource();
            _loopThread = new Thread(RunLoop)
            {
                Name = nameof(Looper),
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
            };

            if (options.AutoStart)
            {
                _loopThread.Start();
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        public void Start()
        {
            if (_loopThread.IsAlive)
            {
                LogInfo($"[{nameof(Looper)}] Loop thread is already running.");
                return;
            }

            _loopThread.Start();
            LogInfo($"[{nameof(Looper)}] Loop thread started.");
        }

        public void Shutdown()
        {
            if (_shutdownRequested)
            {
                LogInfo($"[{nameof(Looper)}] Shutdown already requested.");
                return;
            }
            _shutdownRequested = true;
            LogInfo($"[{nameof(Looper)}] Shutdown requested.");

            _loopCancellationTokenSource.Cancel();
            _loopCancellationTokenSource.Dispose();

            for (var i = 0; i < _disposableActions.Count; i++)
            {
                _disposableActions[i].Action.Invoke();
            }

            _startableActions.Clear();
            _tickableActions.Clear();
            _disposableActions.Clear();

            LogInfo($"[{nameof(Looper)}] Shutdown completed.");
        }

        public void Register(object target)
        {
            if (target is IFrameTimingObserver frameTimingObserver)
            {
                if (!_frameTimingObservers.Contains(frameTimingObserver))
                {
                    _frameTimingObservers.Add(frameTimingObserver);
                }
                else
                {
                    LogInfo($"[{nameof(Looper)}] {target} is already registered as frame timing observer.");
                }
            }

            if (target is IStartable startable)
            {
                var looperAction = new LooperAction(target, startable.Start);
                if (!_startableActions.Contains(looperAction))
                {
                    _startableActions.Add(looperAction);
                }
                else
                {
                    LogInfo($"[{nameof(Looper)}] {target} is already registered as startable.");
                }
            }

            if (target is ITickable tickable)
            {
                var looperAction = new LooperAction(target, tickable.Tick);
                if (!_tickableActions.Contains(looperAction))
                {
                    _tickableActions.Add(looperAction);
                }
                else
                {
                    LogInfo($"[{nameof(Looper)}] {target} is already registered as tickable.");
                }
            }

            if (target is IDisposable disposable)
            {
                var looperAction = new LooperAction(target, disposable.Dispose);
                if (!_disposableActions.Contains(looperAction))
                {
                    _disposableActions.Add(looperAction);
                }
                else
                {
                    LogInfo($"[{nameof(Looper)}] {target} is already registered as disposable.");
                }
            }
        }

        public void Unregister(object target)
        {
            for (var i = 0; i < _frameTimingObservers.Count; i++)
            {
                if (_frameTimingObservers[i] == target)
                {
                    _frameTimingObservers.RemoveAt(i);
                    break;
                }
            }

            for (var i = 0; i < _startableActions.Count; i++)
            {
                if (_startableActions[i].Target == target)
                {
                    _startableActions.RemoveAt(i);
                    break;
                }
            }

            for (var i = 0; i < _tickableActions.Count; i++)
            {
                if (_tickableActions[i].Target == target)
                {
                    _tickableActions.RemoveAt(i);
                    break;
                }
            }

            for (var i = 0; i < _disposableActions.Count; i++)
            {
                if (_disposableActions[i].Target == target)
                {
                    _disposableActions[i].Action.Invoke();
                    _disposableActions.RemoveAt(i);
                    break;
                }
            }
        }

        void RunLoop()
        {
            LogInfo($"[{nameof(Looper)}] RunLoop started. (Thread: {Thread.CurrentThread.ManagedThreadId})");

            while (!_loopCancellationTokenSource.IsCancellationRequested)
            {
                var begin = Stopwatch.GetTimestamp();
                OnBeginFrame(_frameCount);

                OnStart();
                OnTick();

                var end = Stopwatch.GetTimestamp();
                var elapsedTicks = (end - begin) * TimestampsToTicks;
                var elapsedMilliseconds = (long)elapsedTicks / TimeSpan.TicksPerMillisecond;

                _frameCount++;
                OnEndFrame(_frameCount, elapsedMilliseconds);

                var waitForNextFrameMilliseconds = (int)(_targetFrameTimeMilliseconds - elapsedMilliseconds);
                if (waitForNextFrameMilliseconds > 0)
                {
                    Thread.Sleep(waitForNextFrameMilliseconds);
                }
            }

            LogInfo($"[{nameof(Looper)}] RunLoop cancelled.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void OnBeginFrame(ulong frameCount)
        {
            for (var i = 0; i < _frameTimingObservers.Count; i++)
            {
                _frameTimingObservers[i].OnBeginFrame(frameCount);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void OnEndFrame(ulong frameCount, long elapsedMilliseconds)
        {
            for (var i = 0; i < _frameTimingObservers.Count; i++)
            {
                _frameTimingObservers[i].OnEndFrame(frameCount, elapsedMilliseconds);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void OnStart()
        {
            for (var i = 0; i < _startableActions.Count; i++)
            {
                _startableActions[i].Action.Invoke();
            }
            // Reset startable actions to avoid running the same actions multiple times.
            _startableActions.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void OnTick()
        {
            for (var i = 0; i < _tickableActions.Count; i++)
            {
                _tickableActions[i].Action.Invoke();
            }
        }

        void LogInfo(string message)
        {
            _logger?.LogInformation(message);
        }
    }
}
