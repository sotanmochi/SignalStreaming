using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sandbox.StressTest.Client
{
    public sealed class StressTestWorker : ConsoleAppBase
    {
        readonly StressTestWorkerOptions _workerOptions;
        readonly SignalStreamingOptions _signalStreamingOptions;
        readonly SignalStreamingEngine _signalStreamingEngine;
        readonly ILogger<StressTestWorker> _logger;

        public StressTestWorker(
            IOptions<StressTestWorkerOptions> workerOptions,
            IOptions<SignalStreamingOptions> signalStreamingOptions,
            SignalStreamingEngine signalStreamingEngine,
            ILogger<StressTestWorker> logger)
        {
            _workerOptions = workerOptions.Value;
            _signalStreamingOptions = signalStreamingOptions.Value;
            _signalStreamingEngine = signalStreamingEngine;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation($"StressTestWorker.ExecuteAsync() started. (Thread: {Thread.CurrentThread.ManagedThreadId})");

            try
            {
                await _signalStreamingEngine.ConnectAsync(_signalStreamingOptions, Context.CancellationToken);

                var executionTimeSeconds = _workerOptions.ExecutionTimeSeconds >= 0 ? _workerOptions.ExecutionTimeSeconds : -1;
                _logger.LogInformation($"Execution Time: {executionTimeSeconds} [s]");

                var executionTimeMilliseconds = _workerOptions.ExecutionTimeSeconds >= 0 ? _workerOptions.ExecutionTimeSeconds * 1000 : -1;
                await Task.Delay(executionTimeMilliseconds, Context.CancellationToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("TaskCanceledException caught.");
            }
    
            _logger.LogInformation("StressTestWorker.ExecuteAsync() completed.");
        }
    }
}