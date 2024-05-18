using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sandbox.EngineLooper;

namespace Sandbox.Server.SignalStreaming
{
    class LoopHostedService : IHostedService
    {
        readonly Looper _engineLooper;
        readonly IServiceProvider _serviceProvider;
        readonly ILogger<LoopHostedService> _logger;

        public LoopHostedService(Looper engineLooper, IServiceProvider serviceProvider, ILogger<LoopHostedService> logger)
        {
            _engineLooper = engineLooper;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var looperFrameProvider = _serviceProvider.GetRequiredService<LooperFrameProvider>();
            var signalStreamingEngine = _serviceProvider.GetRequiredService<SignalStreamingEngine>();

            _engineLooper.Register(looperFrameProvider);
            _engineLooper.Register(signalStreamingEngine);

            _engineLooper.Start();

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _engineLooper.Shutdown();
        }
    }
}