using System;
using System.Threading;
using System.Threading.Tasks;

namespace SignalStreamingSamples.ConsoleAppClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var looper = new Looper(targetFrameRate: 60);

            // Register event handlers
            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
            {
                Log($"[{nameof(ConsoleAppClient)}] ProcessExit @Thread: {Thread.CurrentThread.ManagedThreadId}");
            };
            AppDomain.CurrentDomain.DomainUnload += (sender, eventArgs) =>
            {
                Log($"[{nameof(ConsoleAppClient)}] DomainUnload @Thread: {Thread.CurrentThread.ManagedThreadId}");
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                looper.Dispose();
                Log($"[{nameof(ConsoleAppClient)}] UnhandledException @Thread: {Thread.CurrentThread.ManagedThreadId}");
                Log(eventArgs.ExceptionObject);
            };
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                looper.Dispose();
                Log($"[{nameof(ConsoleAppClient)}] CancelKeyPress @Thread: {Thread.CurrentThread.ManagedThreadId}"); 
            };

            string connectionKey = "SignalStreaming";
            string serverAddress = "localhost";
            ushort port = 3333;
            string groupId = "01HP8DMTNKAVNQDWCBMG9NWG8S";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--server" && i + 1 < args.Length)
                {
                    serverAddress = args[i + 1];
                }
                else if (args[i] == "--port" && i + 1 < args.Length)
                {
                    port = ushort.Parse(args[i + 1]);
                }
                else if (args[i] == "--group" && i + 1 < args.Length)
                {
                    groupId = args[i + 1];
                }
                else if (args[i] == "--key" && i + 1 < args.Length)
                {
                    connectionKey = args[i + 1];
                }
            }

            Log($"[{nameof(ConsoleAppClient)}] Main @Thread: {Thread.CurrentThread.ManagedThreadId}");

            var client = new SampleClient(serverAddress, port, groupId, connectionKey);
            var metricsExporter = new MetricsExporter(client);

            looper.Register(client);
            looper.Register(metricsExporter);

            var cts = new CancellationTokenSource();
            looper.StartLoop(cts.Token);

            looper.Dispose();
        }

        static void Log(object message)
        {
            Console.WriteLine(message);
        }
    }
}
