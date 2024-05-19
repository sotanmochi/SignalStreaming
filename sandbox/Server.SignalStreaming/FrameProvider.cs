using Sandbox.EngineLooper;

namespace Sandbox.Server.SignalStreaming
{
    public interface IFrameProvider
    {
        ulong FrameCount { get; }
        int LastFrameProcessingTimeMilliseconds { get; }
        int LastFrameDeltaTimeMilliseconds { get; }
    }

    public sealed class LooperFrameProvider : IFrameProvider, IFrameTimingObserver
    {
        ulong _frameCount;
        int _lastFrameProcessingTimeMilliseconds;
        int _lastFrameDeltaTimeMilliseconds;

        public ulong FrameCount => _frameCount;
        public int LastFrameProcessingTimeMilliseconds => _lastFrameProcessingTimeMilliseconds;
        public int LastFrameDeltaTimeMilliseconds => _lastFrameDeltaTimeMilliseconds;

        public void OnBeginFrame(ulong frameCount)
        {
            _frameCount = frameCount;
        }

        public void OnEndFrame(ulong frameCount, int frameProcessingTimeMilliseconds, int frameDeltaTimeMilliseconds)
        {
            _frameCount = frameCount;
            _lastFrameProcessingTimeMilliseconds = frameProcessingTimeMilliseconds;
            _lastFrameDeltaTimeMilliseconds = frameDeltaTimeMilliseconds;
        }
    }
}