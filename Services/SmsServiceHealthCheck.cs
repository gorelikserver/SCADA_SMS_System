using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SCADASMSSystem.Web.Services
{
    public class SmsServiceHealthCheck : IHealthCheck
    {
        private readonly SmsBackgroundService _smsService;
        private readonly ILogger<SmsServiceHealthCheck> _logger;

        public SmsServiceHealthCheck(SmsBackgroundService smsService, ILogger<SmsServiceHealthCheck> logger)
        {
            _smsService = smsService;
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var status = _smsService.GetServiceStatus();
                
                // Check various health indicators
                var isHealthy = true;
                var healthData = new Dictionary<string, object>
                {
                    ["queue_size"] = status.QueueSize,
                    ["messages_sent"] = status.MessagesSent,
                    ["messages_failed"] = status.MessagesFailed,
                    ["service_uptime"] = status.ServiceUptime.ToString(),
                    ["memory_mb"] = status.MemoryUsageMB,
                    ["processing_rate"] = status.ProcessingRatePerMinute
                };

                var issues = new List<string>();

                // Check queue size
                if (status.QueueSize > 100)
                {
                    isHealthy = false;
                    issues.Add($"High queue size: {status.QueueSize}");
                }

                // Check failure rate
                var totalMessages = status.MessagesSent + status.MessagesFailed;
                if (totalMessages > 0)
                {
                    var failureRate = (double)status.MessagesFailed / totalMessages;
                    if (failureRate > 0.1) // More than 10% failure rate
                    {
                        isHealthy = false;
                        issues.Add($"High failure rate: {failureRate:P}");
                    }
                }

                // Check memory usage
                if (status.MemoryUsageMB > 500) // More than 500MB
                {
                    issues.Add($"High memory usage: {status.MemoryUsageMB}MB");
                    // Don't fail health check for memory, just warn
                }

                if (isHealthy)
                {
                    _logger.LogDebug("SMS service health check passed");
                    return Task.FromResult(HealthCheckResult.Healthy("SMS service is operating normally", healthData));
                }
                else
                {
                    _logger.LogWarning("SMS service health check failed: {Issues}", string.Join(", ", issues));
                    return Task.FromResult(HealthCheckResult.Degraded($"SMS service issues detected: {string.Join(", ", issues)}", null, healthData));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing SMS service health check");
                return Task.FromResult(HealthCheckResult.Unhealthy("SMS service health check failed", ex));
            }
        }
    }
}