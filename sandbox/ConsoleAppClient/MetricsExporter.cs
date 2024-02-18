using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SignalStreamingSamples.ConsoleAppClient
{
    public class MetricsExporter : IDisposable, ITickable
    {
        readonly ConcurrentQueue<Metrics> _metricsQueue = new ConcurrentQueue<Metrics>();

        string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffzzz";
        string MetricsDirectory = "./logs";
        string MetricsFilePath;

        SampleClient _client;

        public MetricsExporter(SampleClient client)
        {
            _client = client;

            if (!Directory.Exists(MetricsDirectory))
            {
                Directory.CreateDirectory(MetricsDirectory);
            }
            MetricsFilePath = Path.Combine(MetricsDirectory, $"metrics_{Ulid.NewUlid()}.txt");
        }

        public void Dispose()
        {
            Export();
        }

        public void Tick()
        {
            // Log($"[{nameof(ConsoleAppClient)}] MetricsExporter.Tick (Thread: {Thread.CurrentThread.ManagedThreadId})");

            var metrics = new Metrics()
            {
                FrameCount = _client.FrameCount,
                Timestamp = DateTime.Now.ToString(DateTimeFormat),
            };
            Array.Copy(_client.ReceivedMessageCountByClientId, metrics.ReceivedMessageCountByClientId, SampleClient.MAX_CLIENT_COUNT);
            Array.Copy(_client.TotalPayloadSizeByClientId, metrics.TotalPayloadSizeByClientId, SampleClient.MAX_CLIENT_COUNT);
            Array.Copy(_client.AveragePayloadSizeByClientId, metrics.AveragePayloadSizeByClientId, SampleClient.MAX_CLIENT_COUNT);
            Array.Copy(_client.MaxPayloadSizeByClientId, metrics.MaxPayloadSizeByClientId, SampleClient.MAX_CLIENT_COUNT);
            _metricsQueue.Enqueue(metrics);

            if (_metricsQueue.Count < 10) return;

            Task.Run(() => Export());
        }

        void Export()
        {
            // Log($"[{nameof(ConsoleAppClient)}] MetricsExporter.Export (Thread: {Thread.CurrentThread.ManagedThreadId})");

            var stringBuilder = new StringBuilder();
            while (_metricsQueue.TryDequeue(out var log))
            {
                stringBuilder.Clear();
                stringBuilder.AppendLine($"Metrics data created at {log.Timestamp}");
                stringBuilder.AppendLine($"Frame count: {log.FrameCount}");
                stringBuilder.AppendLine($"----------");
                for (uint i = 0; i < log.ReceivedMessageCountByClientId.Length; i++)
                {
                    stringBuilder.Append($"Client ID: {i, 4:d}, ");
                    stringBuilder.Append($"Received message count: {log.ReceivedMessageCountByClientId[i]}, ");
                    stringBuilder.Append($"Average payload size: {log.AveragePayloadSizeByClientId[i], 6:f} [bytes], ");
                    stringBuilder.Append($"Max payload size: {log.MaxPayloadSizeByClientId[i], 4:d} [bytes], ");
                    stringBuilder.Append($"Total payload size: {log.TotalPayloadSizeByClientId[i]} [bytes]");
                    stringBuilder.AppendLine();
                }
                stringBuilder.AppendLine($"----------");
                stringBuilder.AppendLine($"Output completed at {DateTime.Now.ToString(DateTimeFormat)}");
                File.WriteAllTextAsync(MetricsFilePath, stringBuilder.ToString());
            }
        }

        void Log(object message)
        {
            Console.WriteLine(message);
        }
    }
}
