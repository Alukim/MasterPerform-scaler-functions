using Microsoft.Extensions.Logging;

namespace MasterPerform.Utilities
{
    public static class LoggerExtensions
    {
        public static void LogScaler(this ILogger logger, string message)
            => logger.LogInformation($"MasterPerform - Scaler: {message}");

        public static void LogScalingFunction(this ILogger logger, string message)
            => logger.LogInformation($"MasterPerform - ScalingFunction: {message}");

        public static void LogMetricsCollector(this ILogger logger, string message)
            => logger.LogInformation($"MasterPerform - Metrics Collector: {message}");
    }
}