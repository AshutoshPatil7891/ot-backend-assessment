
namespace OT.Assessment.Consumer.Settings
{
    public class ConsumerSettings
    {
        public int BatchSize { get; set; } = 500;
        public int FlushIntervalMs { get; set; } = 200;
        public int PrefetchCount { get; set; } = 1000;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 500;
        public int MetricsPort { get; set; } = 9101;
    }
}

