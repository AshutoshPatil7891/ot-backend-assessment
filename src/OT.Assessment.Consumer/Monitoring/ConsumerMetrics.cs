using Prometheus;

namespace OT.Assessment.Consumer.Monitoring
{
    /// <summary>
    /// The numbers the Consumer shows on its /metrics page (read by Prometheus, drawn by Grafana).
    /// Process CPU, memory and garbage collection metrics come from prometheus-net on their own.
    /// </summary>
    public static class ConsumerMetrics
    {
        /// <summary>
        /// Call once at startup. .NET creates the metrics below the first time this class is used,
        /// so without this they would be missing from /metrics until the first bet is saved
        /// (Grafana would show "No data" instead of 0).
        /// </summary>
        public static void Initialise()
        {
            BetsSaved.Inc(0);
        }

        public static readonly Counter BetsSaved = Prometheus.Metrics.CreateCounter(
            "ot_consumer_bets_saved_total", "Bets saved to SQL Server and acknowledged.");

        public static readonly Counter Batches = Prometheus.Metrics.CreateCounter(
            "ot_consumer_batches_total", "Batches written to SQL Server.");

        public static readonly Histogram BatchDuration = Prometheus.Metrics.CreateHistogram(
            "ot_consumer_batch_duration_seconds", "Time to save one batch and acknowledge it.");

        public static readonly Counter DeadLettered = Prometheus.Metrics.CreateCounter(
            "ot_consumer_bets_dead_lettered_total", "Messages sent to the dead-letter queue because they can never be saved.");

        public static readonly Counter Requeued = Prometheus.Metrics.CreateCounter(
            "ot_consumer_bets_requeued_total", "Messages put back on the queue because saving failed for now.");

        public static readonly Counter SaveRetries = Prometheus.Metrics.CreateCounter(
            "ot_consumer_save_retries_total", "Times saving a batch was tried again after a temporary failure.");

        public static readonly Gauge BufferedBets = Prometheus.Metrics.CreateGauge(
            "ot_consumer_buffered_bets", "Bets received and waiting in memory to be saved.");
    }
}
