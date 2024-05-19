using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sandbox.EngineLooper;

namespace Sandbox.StressTest.Client;

public static class Program
{
    public static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure<LooperOptions>(options =>
                {
                    options.AutoStart = true;
                    options.TargetFrameRate = 60;
                })
                .AddSingleton<Looper>();

                services.AddSingleton<LooperFrameProvider>();
                services.AddSingleton<IFrameProvider>(sp => sp.GetRequiredService<LooperFrameProvider>());
                services.AddSingleton<IFrameTimingObserver>(sp => sp.GetRequiredService<LooperFrameProvider>());

                services.AddSingleton<SignalStreamingEngine>();

                services.Configure<SignalStreamingOptions>(options =>
                {
                    options.ServerAddress = "localhost";
                    options.ServerPort = 54970;
                    options.ConnectionKey = "SignalStreaming";
                    options.GroupId = "01HP8DMTNKAVNQDWCBMG9NWG8S";
                });

                services.Configure<StressTestWorkerOptions>(options =>
                {
                    options.ExecutionTimeSeconds = -1;
                });
            })
            .RunConsoleAppFrameworkAsync<StressTestWorker>(args);
    }
}