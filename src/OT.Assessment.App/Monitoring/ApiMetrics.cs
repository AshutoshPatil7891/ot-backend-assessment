using Prometheus;

namespace OT.Assessment.App.Monitoring
{
    /// <summary>
    /// The numbers the API shows on its /metrics page (read by Prometheus, drawn by Grafana).
    /// Process CPU, memory and request metrics come from prometheus-net on their own.
    /// </summary>
    public static class ApiMetrics
    {
        /// <summary>
        /// Call once at startup. .NET creates the metrics below the first time this class is used,
        /// so without this they would be missing from /metrics until the first bet arrives
        /// (Grafana would show "No data" instead of 0).
        /// </summary>
        public static void Initialise()
        {
            BetsAccepted.Inc(0);
        }

        public static readonly Counter BetsAccepted = Prometheus.Metrics.CreateCounter(
            "ot_api_bets_accepted_total", "Bets accepted and queued in RabbitMQ.");

        public static readonly Counter BetsRejected = Prometheus.Metrics.CreateCounter(
            "ot_api_bets_rejected_total", "Bets rejected because required fields were missing or invalid.");

        public static readonly Counter BetsPublishFailed = Prometheus.Metrics.CreateCounter(
            "ot_api_bets_publish_failed_total", "Bets that could not be queued (RabbitMQ problem, HTTP 503).");

        public static readonly Histogram PublishDuration = Prometheus.Metrics.CreateHistogram(
            "ot_api_publish_duration_seconds", "Time to queue one bet, including the RabbitMQ confirm.");
    }
}
