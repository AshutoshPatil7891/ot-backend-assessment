using Serilog;
using Serilog.Events;

namespace OT.Assessment.Common.Logging
{
    /// <summary>
    /// Shared logging setup for the API and the Consumer: console output plus one file per day,
    /// with information, warning and error (and fatal) messages in separate folders.
    /// </summary>
    public static class FileLogging
    {
        public static LoggerConfiguration Configure(LoggerConfiguration config, string logsFolder)
        {
            return config
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .WriteTo.Console()
                .WriteTo.Logger(l => l
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Information)
                    .WriteTo.File(Path.Combine(logsFolder, "info", "info-.log"), rollingInterval: RollingInterval.Day))
                .WriteTo.Logger(l => l
                    .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Warning)
                    .WriteTo.File(Path.Combine(logsFolder, "warning", "warning-.log"), rollingInterval: RollingInterval.Day))
                .WriteTo.Logger(l => l
                    .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                    .WriteTo.File(Path.Combine(logsFolder, "error", "error-.log"), rollingInterval: RollingInterval.Day));
        }
    }
}
