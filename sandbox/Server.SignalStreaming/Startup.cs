using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sandbox.EngineLooper;
using Sandbox.Server.SignalStreaming;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHostedService<LoopHostedService>();

        services.Configure<LooperOptions>(options =>
        {
            options.TargetFrameRate = 120;
        })
        .AddSingleton<Looper>();

        services.AddSingleton<LooperFrameProvider>();
        services.AddSingleton<IFrameProvider>(sp => sp.GetRequiredService<LooperFrameProvider>());
        services.AddSingleton<IFrameTimingObserver>(sp => sp.GetRequiredService<LooperFrameProvider>());

        services.Configure<SignalStreamingOptions>(options =>
        {
            // options.Port = 50030;
            options.Port = 54970;
        })
        .AddSingleton<SignalStreamingEngine>();
    }
}