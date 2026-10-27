using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services.Providers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SCADASMSSystem.Web.Services
{
    public class SmsService : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly IOptionsMonitor<SmsSettings> _smsMonitor;
        private readonly IServiceProvider _serviceProvider;
        private readonly SmsCallLog _callLog;
        private readonly SoapXmlLog _soapXmlLog;
        private readonly ILogger<SmsService> _logger;
        private static readonly ConcurrentDictionary<string, DateTime> _lastMessageTimes = new();
        private static DateTime _lastRateLimitReset = DateTime.Now;
        private static int _rateLimitCount;
        private static readonly object _rateLimitLock = new();

        private SmsSettings Settings => _smsMonitor.CurrentValue;

        public SmsService(
            HttpClient httpClient,
            IOptionsMonitor<SmsSettings> smsSettings,
            IServiceProvider serviceProvider,
            SmsCallLog callLog,
            SoapXmlLog soapXmlLog,
            ILogger<SmsService> logger)
        {
            _httpClient = httpClient;
            _smsMonitor = smsSettings;
            _serviceProvider = serviceProvider;
            _callLog = callLog;
            _soapXmlLog = soapXmlLog;
            _logger = logger;

            var timeout = Settings.TimeoutSeconds > 0 ? Settings.TimeoutSeconds : 30;
            _httpClient.Timeout = TimeSpan.FromSeconds(timeout);
        }

        public async Task<bool> SendSmsAsync(string message, IEnumerable<SmsRecipient> recipients, string alarmId, int? groupId = null, string? priority = null, string? value = null)
        {
            try
            {
                var tasks = new List<Task<bool>>();

                foreach (var recipient in recipients)
                {
                    if (!CheckRateLimit())
                    {
                        _logger.LogWarning("Rate limit exceeded, skipping SMS to {PhoneNumber}", MaskPhoneNumber(recipient.PhoneNumber));
                        continue;
                    }

                    if (IsDuplicate(message, recipient.PhoneNumber))
                    {
                        _logger.LogInformation("Duplicate message blocked for {PhoneNumber}", MaskPhoneNumber(recipient.PhoneNumber));
                        continue;
                    }

                    tasks.Add(SendSingleSmsAsync(message, recipient, alarmId, groupId, priority, value));
                }

                if (tasks.Count == 0)
                    return true;

                var results = await Task.WhenAll(tasks);
                return results.All(r => r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SMS for alarm {AlarmId}", alarmId);
                return false;
            }
        }

        public async Task<bool> SendSmsToGroupAsync(string message, int groupId, string alarmId, string? priority = null, string? value = null)
        {
            try
            {
                var userService = _serviceProvider.GetService<IUserService>();
                if (userService == null)
                {
                    _logger.LogError("UserService not available");
                    return false;
                }

                var users = await userService.GetSmsRecipientsForGroupAsync(groupId);
                var smsRecipients = users.Select(u => new SmsRecipient(u.PhoneNumber, u.TZ, u.FirstName, u.LastName)).ToList();

                if (!smsRecipients.Any())
                {
                    _logger.LogWarning("No SMS recipients found for group {GroupId}", groupId);
                    return false;
                }

                _logger.LogInformation("Sending SMS to {Count} recipients in group {GroupId}", smsRecipients.Count, groupId);
                return await SendSmsAsync(message, smsRecipients, alarmId, groupId, priority, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SMS to group {GroupId} for alarm {AlarmId}", groupId, alarmId);
                return false;
            }
        }

        public async Task<CriticalSmsDeliveryResult> SendCriticalSmsToGroupAsync(
            string message,
            int groupId,
            string alarmId,
            string? priority = null,
            string? value = null,
            CancellationToken cancellationToken = default)
        {
            return await SendCriticalSmsToGroupCoreAsync(message, groupId, alarmId, priority, value, cancellationToken);
        }

        private async Task<CriticalSmsDeliveryResult> SendCriticalSmsToGroupCoreAsync(
            string message,
            int groupId,
            string alarmId,
            string? priority,
            string? value,
            CancellationToken cancellationToken)
        {
            var userService = _serviceProvider.GetRequiredService<IUserService>();
            var users = (await userService.GetSmsRecipientsForGroupAsync(groupId)).ToList();
            if (users.Count == 0)
            {
                _logger.LogError("Critical alarm {AlarmId} has no eligible recipients in group {GroupId}", alarmId, groupId);
                return new CriticalSmsDeliveryResult(alarmId, 0, 0, []);
            }

            var recipients = users
                .Select(u => new SmsRecipient(u.PhoneNumber, u.TZ, u.FirstName, u.LastName, u.UserId))
                .ToList();

            using var auditScope = _serviceProvider.CreateScope();
            var auditService = auditScope.ServiceProvider.GetRequiredService<IAuditService>();
            var dbContext = auditScope.ServiceProvider.GetRequiredService<SCADADbContext>();
            // Critical path must fail closed when idempotency state cannot be read.
            var priorAudits = await auditService.GetAuditsByAlarmIdStrictAsync(alarmId);
            var previouslyAcceptedPhones = priorAudits
                .Where(a => a.Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                .Select(a => a.PhoneNumber)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToHashSet(StringComparer.Ordinal);

            var timestampedMessage = AddTimestampToMessage(message);
            var results = new List<SmsRecipientDeliveryResult>(recipients.Count);

            foreach (var recipient in recipients)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var delivery = await ClaimCriticalDeliveryAsync(
                    dbContext, alarmId, groupId, recipient, cancellationToken);

                if (delivery.Status == CriticalDeliveryStatus.Success ||
                    previouslyAcceptedPhones.Contains(recipient.PhoneNumber))
                {
                    results.Add(new SmsRecipientDeliveryResult(
                        recipient.UserId, MaskPhoneNumber(recipient.PhoneNumber), true, 200, 0,
                        "Already accepted for this alarm", true));
                    continue;
                }

                // Failed, indeterminate and stale SENDING rows are re-sent: a duplicate SMS beats a
                // missed alarm. Only a fresh SENDING row (another request is on it) is skipped.
                var staleBefore = CriticalDeliveryPolicy.StaleBefore(DateTime.Now);
                if (delivery.Status == CriticalDeliveryStatus.Sending && delivery.UpdatedAt >= staleBefore)
                {
                    results.Add(new SmsRecipientDeliveryResult(
                        recipient.UserId, MaskPhoneNumber(recipient.PhoneNumber), false,
                        delivery.ProviderStatusCode, delivery.Attempts,
                        "Delivery already in progress for this alarm"));
                    continue;
                }

                var claimed = await dbContext.CriticalSmsDeliveries
                    .Where(d => d.DeliveryId == delivery.DeliveryId)
                    .Where(CriticalDeliveryPolicy.Claimable(staleBefore))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(d => d.Status, CriticalDeliveryStatus.Sending)
                        .SetProperty(d => d.UpdatedAt, DateTime.Now), cancellationToken);

                if (claimed != 1)
                {
                    results.Add(new SmsRecipientDeliveryResult(
                        recipient.UserId, MaskPhoneNumber(recipient.PhoneNumber), false, 0, 0,
                        "Delivery was claimed by another request"));
                    continue;
                }

                await WaitForRateLimitAsync(cancellationToken);

                var maxAttempts = 1 + Math.Max(0, Settings.RetryAttempts);
                SmsApiResponse response = new() { Success = false, Message = "Provider call not attempted" };
                var attempts = 0;

                while (attempts < maxAttempts)
                {
                    attempts++;
                    var context = new SmsCallContext(groupId, alarmId, priority, value);
                    response = await SendSmsApiCallAsync(timestampedMessage, recipient, context, cancellationToken);

                    if (!CriticalDeliveryPolicy.IsRetryable(response) || attempts >= maxAttempts)
                        break;

                    var delay = TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempts - 1));
                    _logger.LogWarning(
                        "Transient SMS failure for alarm {AlarmId}, recipient {PhoneNumber}; retry {Attempt}/{MaxAttempts}",
                        alarmId, MaskPhoneNumber(recipient.PhoneNumber), attempts + 1, maxAttempts);
                    await Task.Delay(delay, cancellationToken);
                    await WaitForRateLimitAsync(cancellationToken);
                }

                var durableStatus = response.Success
                    ? CriticalDeliveryStatus.Success
                    : response.StatusCode == 0
                        ? CriticalDeliveryStatus.Indeterminate
                        : CriticalDeliveryStatus.Failed;

                var stateSaved = await dbContext.CriticalSmsDeliveries
                    .Where(d => d.DeliveryId == delivery.DeliveryId && d.Status == CriticalDeliveryStatus.Sending)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(d => d.Status, durableStatus)
                        .SetProperty(d => d.ProviderStatusCode, response.StatusCode)
                        .SetProperty(d => d.Attempts, attempts)
                        .SetProperty(d => d.ProviderMessage, Truncate(response.Message, 500))
                        .SetProperty(d => d.UpdatedAt, DateTime.Now), cancellationToken);

                var auditSaved = await auditService.LogSmsAuditAsync(
                    alarmId,
                    recipient.UserId,
                    recipient.PhoneNumber,
                    timestampedMessage,
                    response.Success ? "SUCCESS" : "FAILED",
                    response.Message,
                    response.Response,
                    groupId);

                if (!auditSaved)
                    _logger.LogError("Critical delivery audit failed for alarm {AlarmId}, recipient {PhoneNumber}", alarmId, MaskPhoneNumber(recipient.PhoneNumber));

                results.Add(new SmsRecipientDeliveryResult(
                    recipient.UserId,
                    MaskPhoneNumber(recipient.PhoneNumber),
                    response.Success && stateSaved == 1,
                    response.StatusCode,
                    attempts,
                    response.Message));
            }

            var acceptedCount = results.Count(r => r.Accepted);
            return new CriticalSmsDeliveryResult(alarmId, results.Count, acceptedCount, results);
        }

        private static string? Truncate(string? value, int maxLength) =>
            value is null || value.Length <= maxLength ? value : value[..maxLength];

        private static async Task<CriticalSmsDelivery> ClaimCriticalDeliveryAsync(
            SCADADbContext dbContext,
            string alarmId,
            int groupId,
            SmsRecipient recipient,
            CancellationToken cancellationToken)
        {
            var existing = await dbContext.CriticalSmsDeliveries
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    d => d.AlarmId == alarmId && d.PhoneNumber == recipient.PhoneNumber,
                    cancellationToken);
            if (existing != null)
                return existing;

            var delivery = new CriticalSmsDelivery
            {
                AlarmId = alarmId,
                UserId = recipient.UserId,
                GroupId = groupId,
                PhoneNumber = recipient.PhoneNumber,
                Status = CriticalDeliveryStatus.Failed
            };

            dbContext.CriticalSmsDeliveries.Add(delivery);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return delivery;
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                dbContext.Entry(delivery).State = EntityState.Detached;
                return await dbContext.CriticalSmsDeliveries
                    .AsNoTracking()
                    .SingleAsync(
                        d => d.AlarmId == alarmId && d.PhoneNumber == recipient.PhoneNumber,
                        cancellationToken);
            }
        }

        private static string AddTimestampToMessage(string originalMessage)
        {
            try
            {
                var israelTime = TimeZoneInfo.ConvertTime(DateTime.Now,
                    TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time"));
                return $"[{israelTime:dd/MM HH:mm}] {originalMessage}";
            }
            catch
            {
                return $"[{DateTime.Now:dd/MM HH:mm}] {originalMessage}";
            }
        }

        public async Task<SmsApiResponse> SendSmsApiCallAsync(string message, SmsRecipient recipient)
            => await SendSmsApiCallAsync(message, recipient, null);

        // Internal overload that propagates SCADA-derived context (groupId, alarmId, priority, value)
        // so the REST/SOAP keyword resolvers can substitute {GroupId}/{AlarmId}/{Priority}/{Value} tokens.
        private async Task<SmsApiResponse> SendSmsApiCallAsync(string message, SmsRecipient recipient, SmsCallContext? ctx)
            => await SendSmsApiCallAsync(message, recipient, ctx, CancellationToken.None);

        private async Task<SmsApiResponse> SendSmsApiCallAsync(string message, SmsRecipient recipient, SmsCallContext? ctx, CancellationToken cancellationToken)
        {
            var profile = Settings.Profile;

            if (Settings.TestMode)
            {
                var mockService = _serviceProvider.GetService<IMockSmsService>();
                if (mockService != null)
                {
                    _logger.LogInformation("[TEST MODE] Routing SMS to in-process mock for {PhoneNumber}", MaskPhoneNumber(recipient.PhoneNumber));
                    return await mockService.SimulateSmsAsync(message, recipient, profile?.Kind ?? "REST");
                }
                _logger.LogWarning("[TEST MODE] MockSmsService not registered — falling through to real provider");
            }

            // Every provider, REST or SOAP, is driven by the profile: no provider-specific code here.
            var configErrors = ProfileValidator.Validate(profile);
            if (configErrors.Count > 0)
            {
                var reason = "Provider profile is invalid: " + string.Join("; ", configErrors);
                _logger.LogError("SMS to {PhoneNumber} not sent. {Reason}", MaskPhoneNumber(recipient.PhoneNumber), reason);
                return new SmsApiResponse { Success = false, Message = reason, StatusCode = NotSentStatusCode };
            }

            var context = new ProfileContext(
                message, recipient.PhoneNumber, ctx?.Value, ctx?.AlarmId, ctx?.Priority, ctx?.GroupId,
                recipient.UserId, recipient.FirstName, recipient.LastName, recipient.TZ);

            return (await SendWithProfileAsync(profile!, context, cancellationToken)).Response;
        }

        /// <summary>
        /// One provider call with the given profile (active profile for alarms, a draft when an admin
        /// verifies it). No test-mode routing, no profile validation: callers decide.
        /// </summary>
        public async Task<ProfileSendResult> SendWithProfileAsync(ProviderProfile profile, ProfileContext context, CancellationToken cancellationToken = default)
        {
            BuiltRequest built;
            try
            {
                built = ProviderRequestBuilder.Build(profile, context);
            }
            catch (Exception ex) when (ex is TemplateException or FormatException)
            {
                _logger.LogError("SMS to {PhoneNumber} not sent: {Reason}", MaskPhoneNumber(context.Phone), ex.Message);
                return new ProfileSendResult(new SmsApiResponse { Success = false, Message = ex.Message, StatusCode = NotSentStatusCode }, "", "");
            }

            using (built)
            {
                if (profile.Kind.Equals("SOAP", StringComparison.OrdinalIgnoreCase) && Settings.LogSoapXml)
                    _soapXmlLog.Push(MaskPhoneNumber(context.Phone), built.DisplayBody);

                var sw = Stopwatch.StartNew();
                try
                {
                    using var response = await _httpClient.SendAsync(built.Request, cancellationToken);
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    sw.Stop();

                    var verdict = ResponseEvaluator.Evaluate(profile, (int)response.StatusCode, body);
                    if (verdict.Accepted)
                        _logger.LogInformation("SMS accepted for {PhoneNumber}: {Reason}", MaskPhoneNumber(context.Phone), verdict.Reason);
                    else
                        _logger.LogError("SMS rejected for {PhoneNumber}: {Reason}. Response: {Body}", MaskPhoneNumber(context.Phone), verdict.Reason, body);

                    _callLog.Add(new SmsCallEntry
                    {
                        Timestamp      = DateTime.Now,
                        Provider       = profile.Kind,
                        Endpoint       = built.DisplayUrl,
                        HttpMethod     = built.Request.Method.Method,
                        PhoneNumber    = MaskPhoneNumber(context.Phone),
                        AlarmId        = context.AlarmId ?? string.Empty,
                        RequestSummary = Truncate(built.DisplayBody, 2000) ?? string.Empty,
                        FullResponse   = body,
                        StatusCode     = (int)response.StatusCode,
                        Success        = verdict.Accepted,
                        DurationMs     = sw.ElapsedMilliseconds
                    });

                    return new ProfileSendResult(new SmsApiResponse
                    {
                        Success = verdict.Accepted,
                        Message = verdict.Reason,
                        Response = body,
                        StatusCode = (int)response.StatusCode
                    }, built.DisplayUrl, built.DisplayBody);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    // Timeout or transport loss: the provider may or may not have sent it (status 0 = indeterminate).
                    _logger.LogError(ex, "SMS call to {PhoneNumber} failed in transport", MaskPhoneNumber(context.Phone));
                    _callLog.Add(new SmsCallEntry
                    {
                        Timestamp    = DateTime.Now,
                        Provider     = profile.Kind,
                        Endpoint     = built.DisplayUrl,
                        HttpMethod   = built.Request.Method.Method,
                        PhoneNumber  = MaskPhoneNumber(context.Phone),
                        AlarmId      = context.AlarmId ?? string.Empty,
                        FullResponse = ex.Message,
                        Success      = false,
                        StatusCode   = 0
                    });
                    return new ProfileSendResult(new SmsApiResponse { Success = false, Message = ex.Message, Response = ex.ToString(), StatusCode = 0 },
                        built.DisplayUrl, built.DisplayBody);
                }
            }
        }

        // Nothing was sent (configuration problem): a plain failure, not "indeterminate" (status 0).
        public const int NotSentStatusCode = -1;

        private async Task<bool> SendSingleSmsAsync(string message, SmsRecipient recipient, string alarmId, int? groupId = null, string? priority = null, string? value = null)
        {
            var phoneNumber = recipient.PhoneNumber;
            try
            {
                var ctx = new SmsCallContext(groupId, alarmId, priority, value);
                var response = await SendSmsApiCallAsync(message, recipient, ctx);
                
                // CRITICAL: Create dedicated scope for audit logging to ensure proper database transaction
                using var auditScope = _serviceProvider.CreateScope();
                var auditService = auditScope.ServiceProvider.GetRequiredService<IAuditService>();
                var userService = auditScope.ServiceProvider.GetRequiredService<IUserService>();

                _logger.LogDebug("=== AUDIT LOGGING SCOPE CREATED ===");
                _logger.LogDebug("Audit scope created for alarm {AlarmId}, phone {PhoneNumber}, group {GroupId}", alarmId, MaskPhoneNumber(phoneNumber), groupId);

                // Find user by phone number for audit logging
                int userId = 0;
                try
                {
                    var users = await userService.GetAllUsersAsync();
                    var user = users.FirstOrDefault(u => u.PhoneNumber == phoneNumber);
                    userId = user?.UserId ?? 0;
                    _logger.LogDebug("Found user ID {UserId} for phone {PhoneNumber}", userId, MaskPhoneNumber(phoneNumber));
                }
                catch (Exception userEx)
                {
                    _logger.LogWarning(userEx, "Could not find user for phone {PhoneNumber}, using userId=0", MaskPhoneNumber(phoneNumber));
                    userId = 0;
                }

                // CRITICAL AUDIT LOGGING - This MUST succeed
                try
                {
                    _logger.LogInformation("=== STARTING CRITICAL AUDIT LOGGING ===");
                    _logger.LogInformation("AlarmId: {AlarmId}", alarmId);
                    _logger.LogInformation("UserId: {UserId}", userId);
                    _logger.LogInformation("GroupId: {GroupId}", groupId);
                    _logger.LogInformation("PhoneNumber: {PhoneNumber}", MaskPhoneNumber(phoneNumber));
                    _logger.LogInformation("Message: {Message}", message);
                    _logger.LogInformation("Status: {Status}", response.Success ? "SUCCESS" : "FAILED");

                    var auditSuccess = await auditService.LogSmsAuditAsync(
                        alarmId,
                        userId,
                        phoneNumber,
                        message,
                        response.Success ? "SUCCESS" : "FAILED",
                        response.Message,
                        response.Response,
                        groupId
                    );

                    if (auditSuccess)
                    {
                        _logger.LogInformation("AUDIT LOGGING SUCCESSFUL for alarm {AlarmId}", alarmId);
                    }
                    else
                    {
                        _logger.LogError("AUDIT LOGGING FAILED for alarm {AlarmId} - LogSmsAuditAsync returned false", alarmId);
                    }
                }
                catch (Exception auditEx)
                {
                    _logger.LogError(auditEx, "CRITICAL EXCEPTION during audit logging for alarm {AlarmId} - Exception: {ExceptionType}: {ExceptionMessage}", 
                        alarmId, auditEx.GetType().Name, auditEx.Message);
                    
                    if (auditEx.InnerException != null)
                    {
                        _logger.LogError("Inner exception: {InnerType}: {InnerMessage}", 
                            auditEx.InnerException.GetType().Name, auditEx.InnerException.Message);
                    }
                }

                _logger.LogDebug("=== AUDIT LOGGING SCOPE DISPOSING ===");

                // Update duplicate tracking (outside audit scope)
                if (response.Success)
                {
                    UpdateDuplicateTracking(message, phoneNumber);
                }

                return response.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FATAL ERROR in SendSingleSmsAsync for {PhoneNumber}, alarm {AlarmId}", MaskPhoneNumber(phoneNumber), alarmId);
                throw; // Re-throw to ensure background service counts this as failed
            }
        }

        private string MaskPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
                return phoneNumber;
            
            // Keep first 2 and last 2 digits, mask the middle
            if (phoneNumber.Length <= 6)
            {
                return $"{phoneNumber.Substring(0, 2)}***{phoneNumber.Substring(phoneNumber.Length - 2)}";
            }
            
            return $"{phoneNumber.Substring(0, 3)}***{phoneNumber.Substring(phoneNumber.Length - 3)}";
        }

        private bool CheckRateLimit() => TryAcquireRateLimit();

        private bool TryAcquireRateLimit()
        {
            var now = DateTime.Now;

            lock (_rateLimitLock)
            {
                if ((now - _lastRateLimitReset).TotalSeconds >= Settings.RateWindow)
                {
                    _lastRateLimitReset = now;
                    _rateLimitCount = 0;
                }

                if (_rateLimitCount >= Math.Max(1, Settings.RateLimit))
                    return false;

                _rateLimitCount++;
                return true;
            }
        }

        private async Task WaitForRateLimitAsync(CancellationToken cancellationToken)
        {
            while (!TryAcquireRateLimit())
            {
                var elapsed = DateTime.Now - _lastRateLimitReset;
                var waitSeconds = Math.Max(0.1, Settings.RateWindow - elapsed.TotalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(waitSeconds, 1)), cancellationToken);
            }
        }

        private bool IsDuplicate(string message, string phoneNumber)
        {
            var key = $"{phoneNumber}:{message.GetHashCode()}";
            
            if (_lastMessageTimes.TryGetValue(key, out DateTime lastTime))
            {
                var minutesSinceLastMessage = (DateTime.Now - lastTime).TotalMinutes;
                if (minutesSinceLastMessage < Settings.DuplicateWindow)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateDuplicateTracking(string message, string phoneNumber)
        {
            var key = $"{phoneNumber}:{message.GetHashCode()}";
            _lastMessageTimes[key] = DateTime.Now;

            // Clean up old entries (older than duplicate window)
            var cutoffTime = DateTime.Now.AddMinutes(-Settings.DuplicateWindow);
            var keysToRemove = _lastMessageTimes
                .Where(kvp => kvp.Value < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var keyToRemove in keysToRemove)
            {
                _lastMessageTimes.TryRemove(keyToRemove, out _);
            }
        }
    }

    /// <summary>
    /// Carries SCADA-derived metadata (group_id, alarm_id, priority, value) through the
    /// SmsService send pipeline so REST/SOAP keyword resolvers can substitute the matching
    /// dynamic placeholders. Pass null when invoking the public SendSmsApiCallAsync overload
    /// from a context that does not have these fields (e.g. test pages).
    /// </summary>
    public sealed record ProfileSendResult(SmsApiResponse Response, string DisplayUrl, string DisplayBody);

    public sealed record SmsCallContext(int? GroupId, string? AlarmId, string? Priority, string? Value);
}
