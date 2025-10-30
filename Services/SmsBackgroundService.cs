using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SCADASMSSystem.Web.Services
{
    public class SmsBackgroundService : BackgroundService, ISmsBackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SmsBackgroundService> _logger;
        private readonly SmsSettings _smsSettings;
        private readonly ConcurrentQueue<SmsQueueItem> _messageQueue = new();
        private readonly SemaphoreSlim _processingSemaphore;
        
        // Statistics tracking
        private int _messagesSent = 0;
        private int _messagesFailed = 0;
        private int _duplicatesBlocked = 0;
        private int _rateLimited = 0;
        private DateTime _serviceStartTime = DateTime.Now;
        private DateTime? _lastMessageTime;

        public SmsBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<SmsBackgroundService> logger,
            IOptions<SmsSettings> smsSettings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _smsSettings = smsSettings.Value;
            _processingSemaphore = new SemaphoreSlim(_smsSettings.RateLimit, _smsSettings.RateLimit);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SMS Background Service started at {StartTime}", _serviceStartTime);
            var lastResetMinute = DateTime.Now.Minute;
            
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await ProcessMessageQueue(stoppingToken);
                    
                    // Reset rate limiting semaphore every minute
                    var currentMinute = DateTime.Now.Minute;
                    if (currentMinute != lastResetMinute)
                    {
                        ResetRateLimitingSemaphore();
                        lastResetMinute = currentMinute;
                    }

                    // Small delay to prevent excessive CPU usage
                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SMS Background Service is stopping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in SMS Background Service");
                throw; // Re-throw to restart the service
            }
        }

        public async Task<bool> QueueSmsMessageAsync(string message, int groupId, string alarmId, string priority = "normal")
        {
            try
            {
                var queueItem = new SmsQueueItem
                {
                    Message = message,
                    GroupId = groupId,
                    AlarmId = alarmId,
                    Priority = priority,
                    QueuedAt = DateTime.Now
                };

                _messageQueue.Enqueue(queueItem);
                _logger.LogInformation("Queued SMS message for group {GroupId}, alarm {AlarmId}", groupId, alarmId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing SMS message for group {GroupId}", groupId);
                return false;
            }
        }

        public SmsServiceStatus GetServiceStatus()
        {
            return new SmsServiceStatus
            {
                QueueSize = _messageQueue.Count,
                MessagesSent = _messagesSent,
                MessagesFailed = _messagesFailed,
                DuplicatesBlocked = _duplicatesBlocked,
                RateLimited = _rateLimited,
                ServiceUptime = DateTime.Now - _serviceStartTime,
                LastMessageTime = _lastMessageTime,
                DeduplicationEnabled = true,
                RateLimit = _smsSettings.RateLimit,
                DuplicateWindow = _smsSettings.DuplicateWindow,
                IsHealthy = _messageQueue.Count < 100 && _messagesFailed < (_messagesSent * 0.05), // Less than 5% failure rate
                ProcessingRatePerMinute = CalculateProcessingRate(),
                MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024),
                ThreadCount = System.Diagnostics.Process.GetCurrentProcess().Threads.Count
            };
        }

        private double CalculateProcessingRate()
        {
            try
            {
                var uptimeMinutes = (DateTime.Now - _serviceStartTime).TotalMinutes;
                return uptimeMinutes > 0 ? _messagesSent / uptimeMinutes : 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task ProcessMessageQueue(CancellationToken cancellationToken)
        {
            if (_messageQueue.TryDequeue(out var queueItem))
            {
                try
                {
                    // Wait for rate limiting semaphore
                    await _processingSemaphore.WaitAsync(cancellationToken);
                    
                    try
                    {
                        await ProcessSingleMessage(queueItem);
                        _lastMessageTime = DateTime.Now;
                    }
                    finally
                    {
                        // Don't release the semaphore immediately - this maintains rate limiting
                        // The semaphore will be reset every minute by ResetRateLimitingSemaphore()
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing SMS message for alarm {AlarmId}", queueItem.AlarmId);
                    Interlocked.Increment(ref _messagesFailed);
                }
            }
        }

        private async Task ProcessSingleMessage(SmsQueueItem queueItem)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var smsService = scope.ServiceProvider.GetRequiredService<ISmsService>();
                
                _logger.LogInformation("Processing SMS for group {GroupId}, alarm {AlarmId}: {Message}", 
                    queueItem.GroupId, queueItem.AlarmId, queueItem.Message);

                // Add timestamp to message if configured
                var messageWithTimestamp = AddTimestampToMessage(queueItem.Message);
                
                var success = await smsService.SendSmsToGroupAsync(messageWithTimestamp, queueItem.GroupId, queueItem.AlarmId);
                
                if (success)
                {
                    Interlocked.Increment(ref _messagesSent);
                    _logger.LogInformation("Successfully processed SMS for alarm {AlarmId} to group {GroupId}", 
                        queueItem.AlarmId, queueItem.GroupId);
                }
                else
                {
                    Interlocked.Increment(ref _messagesFailed);
                    _logger.LogWarning("Failed to process SMS for alarm {AlarmId} to group {GroupId}", 
                        queueItem.AlarmId, queueItem.GroupId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessSingleMessage for alarm {AlarmId}", queueItem.AlarmId);
                Interlocked.Increment(ref _messagesFailed);
                throw;
            }
        }

        private string AddTimestampToMessage(string originalMessage)
        {
            try
            {
                // Add Israel timezone timestamp similar to Python implementation
                var israelTime = TimeZoneInfo.ConvertTime(DateTime.Now, 
                    TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time"));
                
                var timestamp = israelTime.ToString("[dd/MM HH:mm]");
                return $"{timestamp} {originalMessage}";
            }
            catch
            {
                // Fallback to local time if Israel timezone not available
                var timestamp = DateTime.Now.ToString("[dd/MM HH:mm]");
                return $"{timestamp} {originalMessage}";
            }
        }

        private void ResetRateLimitingSemaphore()
        {
            try
            {
                // Release all waiting threads and reset the semaphore for the new minute
                var currentCount = _processingSemaphore.CurrentCount;
                var maxCount = _smsSettings.RateLimit;
                
                if (currentCount < maxCount)
                {
                    _processingSemaphore.Release(maxCount - currentCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting rate limiting semaphore");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SMS Background Service is stopping. Processing remaining messages...");
            
            // Process remaining messages in queue before stopping
            var timeout = TimeSpan.FromSeconds(30);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            try
            {
                while (_messageQueue.Count > 0 && !cts.Token.IsCancellationRequested)
                {
                    await ProcessMessageQueue(cts.Token);
                    await Task.Delay(10, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Timeout reached while processing remaining messages. {Count} messages remain in queue.", 
                    _messageQueue.Count);
            }

            await base.StopAsync(cancellationToken);
            _logger.LogInformation("SMS Background Service stopped. Final stats - Sent: {Sent}, Failed: {Failed}", 
                _messagesSent, _messagesFailed);
        }
    }

    public class SmsQueueItem
    {
        public string Message { get; set; } = string.Empty;
        public int GroupId { get; set; }
        public string AlarmId { get; set; } = string.Empty;
        public string Priority { get; set; } = "normal";
        public DateTime QueuedAt { get; set; }
    }

    public class SmsServiceStatus
    {
        public int QueueSize { get; set; }
        public int MessagesSent { get; set; }
        public int MessagesFailed { get; set; }
        public int DuplicatesBlocked { get; set; }
        public int RateLimited { get; set; }
        public TimeSpan ServiceUptime { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public bool DeduplicationEnabled { get; set; }
        public int RateLimit { get; set; }
        public int DuplicateWindow { get; set; }
        public bool IsHealthy { get; set; }
        public double ProcessingRatePerMinute { get; set; }
        public long MemoryUsageMB { get; set; }
        public int ThreadCount { get; set; }

        public string GetHealthBadgeClass()
        {
            if (!IsHealthy) return "badge-danger";
            if (QueueSize > 50) return "badge-warning"; 
            return "badge-success";
        }

        public string GetHealthStatusText()
        {
            if (!IsHealthy) return "Unhealthy";
            if (QueueSize > 50) return "Degraded";
            return "Healthy";
        }
    }
}