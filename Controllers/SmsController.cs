using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCADASMSSystem.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SCADASMSSystem.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous] // called by Scada_sms.bat / the app itself, never by a signed-in browser
    public class SmsController : ControllerBase
    {
        private readonly SmsBackgroundService _smsBackgroundService;
        private readonly ISmsService _smsService;
        private readonly IAlertNotifier _alertNotifier;
        private readonly ILogger<SmsController> _logger;

        public SmsController(SmsBackgroundService smsBackgroundService, ISmsService smsService, IAlertNotifier alertNotifier, ILogger<SmsController> logger)
        {
            _smsBackgroundService = smsBackgroundService;
            _smsService = smsService;
            _alertNotifier = alertNotifier;
            _logger = logger;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendSms([FromBody] SmsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (string.IsNullOrWhiteSpace(request.AlarmId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "AlarmId is required for critical delivery and must remain stable across retries"
                    });
                }

                var alarmId = request.AlarmId;
                var result = await _smsService.SendCriticalSmsToGroupAsync(
                    request.Message,
                    request.GroupId,
                    alarmId,
                    request.Priority ?? "normal",
                    request.Value,
                    CancellationToken.None);

                var response = new
                {
                    success = result.Success,
                    alarm_id = result.AlarmId,
                    recipients = result.RecipientCount,
                    accepted = result.AcceptedCount,
                    failed = result.FailedCount,
                    deliveries = result.Recipients
                };

                if (result.Success)
                {
                    _logger.LogInformation("Critical alarm {AlarmId} accepted for all {RecipientCount} recipients", alarmId, result.RecipientCount);
                    return Ok(response);
                }

                _logger.LogError("Critical alarm {AlarmId} failed or partially failed: {Accepted}/{RecipientCount} accepted", alarmId, result.AcceptedCount, result.RecipientCount);
                _alertNotifier.DeliveryIncomplete(alarmId, request.GroupId, result.AcceptedCount, result.RecipientCount);
                return StatusCode(StatusCodes.Status502BadGateway, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendSms API endpoint");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            try
            {
                var status = _smsBackgroundService.GetServiceStatus();
                
                return Ok(new
                {
                    queue_size = status.QueueSize,
                    messages_sent = status.MessagesSent,
                    messages_failed = status.MessagesFailed,
                    duplicates_blocked = status.DuplicatesBlocked,
                    rate_limited = status.RateLimited,
                    service_uptime = status.ServiceUptime.ToString(@"d\.hh\:mm\:ss"),
                    last_message_time = status.LastMessageTime?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    deduplication_enabled = status.DeduplicationEnabled,
                    rate_limit = status.RateLimit,
                    duplicate_window = status.DuplicateWindow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting service status");
                return StatusCode(500, new { error = "Failed to get service status" });
            }
        }

        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            try
            {
                var status = _smsBackgroundService.GetServiceStatus();
                var isHealthy = status.QueueSize < 100; // Configurable threshold

                var healthResponse = new
                {
                    status = isHealthy ? "healthy" : "degraded",
                    timestamp = DateTime.Now,
                    queue_size = status.QueueSize,
                    service_uptime = status.ServiceUptime.ToString(@"d\.hh\:mm\:ss"),
                    messages_sent_last_hour = status.MessagesSent, // Could be refined to actual last hour
                    deduplication_enabled = status.DeduplicationEnabled,
                    details = new
                    {
                        rate_limit = status.RateLimit,
                        duplicate_window = status.DuplicateWindow,
                        last_message_time = status.LastMessageTime?.ToString("yyyy-MM-ddTHH:mm:ss")
                    }
                };

                return isHealthy ? Ok(healthResponse) : StatusCode(503, healthResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing health check");
                return StatusCode(503, new { 
                    status = "unhealthy", 
                    timestamp = DateTime.Now,
                    error = "Health check failed" 
                });
            }
        }

        [HttpPost("test")]
        public async Task<IActionResult> SendTestMessage([FromBody] SmsTestRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new { success = false, message = "Test message is required" });
                }

                var testAlarmId = $"TEST-{DateTime.Now:yyyyMMddHHmmss}";
                var success = await _smsBackgroundService.QueueSmsMessageAsync(
                    $"[TEST] {request.Message}", 
                    request.GroupId, 
                    testAlarmId, 
                    "test");

                if (success)
                {
                    _logger.LogInformation("Test SMS queued successfully for group {GroupId}, alarm {AlarmId}", request.GroupId, testAlarmId);
                    return Ok(new { 
                        success = true, 
                        message = "Test SMS queued successfully",
                        test_alarm_id = testAlarmId,
                        note = "Test messages are prefixed with [TEST] for identification"
                    });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to queue test SMS" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test SMS");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpPost("queue")]
        [HttpPost("")]  // Root endpoint for compatibility with Python service
        public async Task<IActionResult> QueueMessage([FromBody] SmsRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = await _smsBackgroundService.QueueSmsMessageAsync(
                request.Message,
                request.GroupId,
                request.AlarmId ?? $"QUEUE-{Guid.NewGuid():N}",
                request.Priority ?? "normal",
                request.Value);

            return success
                ? Accepted(new { success = true, message = "SMS queued successfully" })
                : StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { success = false, message = "Failed to queue SMS" });
        }
    }

    // Legacy controller for root-level endpoints (Python compatibility)
    [ApiController]
    [Route("")]
    [AllowAnonymous] // called by Scada_sms.bat / the app itself, never by a signed-in browser
    public class LegacySmsController : ControllerBase
    {
        private readonly SmsBackgroundService _smsBackgroundService;
        private readonly ILogger<LegacySmsController> _logger;

        public LegacySmsController(SmsBackgroundService smsBackgroundService, ILogger<LegacySmsController> logger)
        {
            _smsBackgroundService = smsBackgroundService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> QueueMessage([FromBody] SmsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var success = await _smsBackgroundService.QueueSmsMessageAsync(
                    request.Message, 
                    request.GroupId, 
                    request.AlarmId ?? Guid.NewGuid().ToString(), 
                    request.Priority ?? "normal",
                    request.Value);

                if (success)
                {
                    _logger.LogInformation("SMS queued successfully for group {GroupId} (legacy endpoint)", request.GroupId);
                    return Ok(new { success = true, message = "Message queued successfully" });
                }
                else
                {
                    return BadRequest(new { success = false, message = "Failed to queue message" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in legacy SMS endpoint");
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            try
            {
                var status = _smsBackgroundService.GetServiceStatus();
                
                return Ok(new
                {
                    queue_size = status.QueueSize,
                    messages_sent = status.MessagesSent,
                    messages_failed = status.MessagesFailed,
                    duplicates_blocked = status.DuplicatesBlocked,
                    rate_limited = status.RateLimited,
                    service_uptime = status.ServiceUptime.ToString(@"d\.hh\:mm\:ss"),
                    last_message_time = status.LastMessageTime?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    deduplication_enabled = status.DeduplicationEnabled
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting service status (legacy endpoint)");
                return StatusCode(500, new { error = "Failed to get service status" });
            }
        }
    }

    public class SmsRequest
    {
        [Required]
        [StringLength(1000, MinimumLength = 1)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "GroupId must be a positive integer")]
        public int GroupId { get; set; }

        public string? AlarmId { get; set; }

        [RegularExpression("^(normal|urgent|critical)$", ErrorMessage = "Priority must be normal, urgent, or critical")]
        public string? Priority { get; set; } = "normal";

        // Optional measurement value forwarded by SCADA (e.g. tag reading at the time of the alarm).
        // Available as the {Value} / value dynamic placeholder in REST and SOAP body templates.
        public string? Value { get; set; }
    }

    public class SmsTestRequest
    {
        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "GroupId must be a positive integer")]
        public int GroupId { get; set; }
    }
}
