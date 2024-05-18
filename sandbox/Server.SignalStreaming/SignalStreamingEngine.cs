using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sandbox.EngineLooper;

namespace Sandbox.Server.SignalStreaming
{
    public sealed class SignalStreamingEngine : IDisposable, IStartable, ITickable
    {
        readonly SignalStreamingOptions _options;
        readonly IFrameProvider _frameProvider;
        readonly ILogger<SignalStreamingEngine> _logger;

        public SignalStreamingEngine(
            IOptions<SignalStreamingOptions> options,
            IFrameProvider frameProvider,
            ILogger<SignalStreamingEngine> logger) : this(options.Value, frameProvider, logger)
        {
        }

        public SignalStreamingEngine(
            SignalStreamingOptions options,
            IFrameProvider frameProvider,
            ILogger<SignalStreamingEngine> logger = null)
        {
            _options = options;
            _frameProvider = frameProvider;
            _logger = logger;
        }

        public void Dispose()
        {
            LogInfo("Dispose");
        }

        void IStartable.Start()
        {
            LogInfo("IStartable.Start");
        }

        void ITickable.Tick()
        {
            System.Threading.Thread.Sleep(3);

            var tickMessage = $"FrameCount: {_frameProvider.FrameCount}, "
                            + $"LastFrameDeltaTimeMilliseconds: {_frameProvider.LastFrameDeltaTimeMilliseconds}";

            LogInfo($"ITickable.Tick - {tickMessage}");
        }

        void LogInfo(string message)
        {
            _logger?.LogInformation($"[{nameof(SignalStreamingEngine)}] {message}");
        }
    }
}