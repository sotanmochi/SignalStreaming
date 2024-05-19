using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sandbox.EngineLooper;

namespace Sandbox.StressTest.Client;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var serverAddress = "localhost";
        var serverPort = 54970;
        var connectionKey = "SignalStreaming";
        var groupId = "01HP8DMTNKAVNQDWCBMG9NWG8S";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--server" && i + 1 < args.Length)
            {
                serverAddress = args[i + 1];
            }
            else if (args[i] == "--port" && i + 1 < args.Length)
            {
                serverPort = ushort.Parse(args[i + 1]);
            }
            else if (args[i] == "--key" && i + 1 < args.Length)
            {
                connectionKey = args[i + 1];
            }
            else if (args[i] == "--group" && i + 1 < args.Length)
            {
                groupId = args[i + 1];
            }
        }

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
                    options.ServerAddress = serverAddress;
                    options.ServerPort = (ushort)serverPort;
                    options.ConnectionKey = connectionKey;
                    options.GroupId = groupId;
                });

                services.Configure<StressTestWorkerOptions>(options =>
                {
                    options.ExecutionTimeSeconds = -1;
                });
            })
            .RunConsoleAppFrameworkAsync<StressTestWorker>(args);
    }
}