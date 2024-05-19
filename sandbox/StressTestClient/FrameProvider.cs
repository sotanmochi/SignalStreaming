using Sandbox.EngineLooper;

namespace Sandbox.StressTest.Client
{
    public interface IFrameProvider
    {
        ulong FrameCount { get; }
        long LastFrameDeltaTimeMilliseconds { get; }
    }

    public sealed class LooperFrameProvider : IFrameProvider, IFrameTimingObserver
    {
        ulong _frameCount;
        long _lastFrameDeltaTimeMilliseconds;

        public ulong FrameCount => _frameCount;
        public long LastFrameDeltaTimeMilliseconds => _lastFrameDeltaTimeMilliseconds;

        public void OnBeginFrame(ulong frameCount)
        {
            _frameCount = frameCount;
        }

        public void OnEndFrame(ulong frameCount, long elapsedMilliseconds)
        {
            _frameCount = frameCount;
            _lastFrameDeltaTimeMilliseconds = elapsedMilliseconds;
        }
    }
}