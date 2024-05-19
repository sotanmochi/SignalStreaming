namespace Sandbox.EngineLooper
{
    public interface IFrameTimingObserver
    {
        void OnBeginFrame(ulong frameCount);
        void OnEndFrame(ulong frameCount, int frameProcessingTimeMilliseconds, int frameDeltaTimeMilliseconds);
    }
}