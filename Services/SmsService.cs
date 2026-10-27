using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;
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

                if (delivery.Status is CriticalDeliveryStatus.Sending or CriticalDeliveryStatus.Indeterminate)
                {
                    results.Add(new SmsRecipientDeliveryResult(
                        recipient.UserId, MaskPhoneNumber(recipient.PhoneNumber), false,
                        delivery.ProviderStatusCode, delivery.Attempts,
                        "Delivery already active or outcome is indeterminate"));
                    continue;
                }

                var claimed = await dbContext.CriticalSmsDeliveries
                    .Where(d => d.DeliveryId == delivery.DeliveryId && d.Status == CriticalDeliveryStatus.Failed)
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

                    if (response.Success || !IsTransientFailure(response) || attempts >= maxAttempts)
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

        private static bool IsTransientFailure(SmsApiResponse response) =>
            // Retry only explicit provider throttling. Timeout, transport loss, and 5xx
            // can occur after acceptance and are unsafe without provider idempotency.
            response.StatusCode == 429;

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
            if (Settings.TestMode)
            {
                var mockService = _serviceProvider.GetService<IMockSmsService>();
                if (mockService != null)
                {
                    _logger.LogInformation("[TEST MODE] Routing SMS to in-process mock for {PhoneNumber}", MaskPhoneNumber(recipient.PhoneNumber));
                    return await mockService.SimulateSmsAsync(message, recipient, Settings.ProviderType);
                }
                _logger.LogWarning("[TEST MODE] MockSmsService not registered — falling through to real provider");
            }

            if (Settings.ProviderType?.Equals("SOAP", StringComparison.OrdinalIgnoreCase) == true)
                return await SendSoapApiCallAsync(message, recipient, ctx, cancellationToken);

            var phoneNumber = recipient.PhoneNumber;

            try
            {
                var requestParams = new Dictionary<string, string>();

                // Phase E: detect whether any param row resolves to the recipient's phone number.
                // Phone is no longer a forced/locked mapping in Settings — admins choose the param key
                // their gateway expects. If no row maps `phone` we still send (some providers might
                // derive phone from a header or path), but we log a warning so the operator notices.
                bool anyPhoneMapped = false;

                if (Settings.RestBodyParams.Count > 0)
                {
                    // New dynamic list path
                    foreach (var param in Settings.RestBodyParams)
                    {
                        string resolvedValue;
                        var kw = param.Value.ToLowerInvariant();
                        if (kw is "message" or "msg" or "text" or "body")
                            resolvedValue = message;
                        else
                        {
                            resolvedValue = ResolveRestKeyword(param.Value, phoneNumber, recipient, ctx);
                        }
                        if (kw is "phone" or "phonenumber" or "sendtophonenumbers" or "mobile" or "number")
                            anyPhoneMapped = true;
                        requestParams[param.Key] = resolvedValue;
                    }
                }
                else
                {
                    // Legacy: parse ApiParams JSON string
                    var apiParams = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings.ApiParams ?? "{}");
                    if (apiParams == null)
                        return new SmsApiResponse { Success = false, Message = "Invalid API parameters configuration" };

                    foreach (var param in apiParams)
                    {
                        string paramValue;
                        switch (param.Value.ToLower())
                        {
                            case "message":
                            case "msg":
                            case "text":
                            case "body":
                                paramValue = message;
                                break;
                            case "phone":
                            case "phonenumber":
                            case "sendtophonenumbers":
                            case "mobile":
                            case "number":
                                paramValue = phoneNumber;
                                anyPhoneMapped = true;
                                break;
                            case "tz":
                                paramValue = recipient.TZ ?? "";
                                break;
                            case "firstname":
                                paramValue = recipient.FirstName ?? "";
                                break;
                            case "lastname":
                                paramValue = recipient.LastName ?? "";
                                break;
                            case "group_id":
                            case "groupid":
                                paramValue = ctx?.GroupId?.ToString() ?? "";
                                break;
                            case "alarm_id":
                            case "alarmid":
                                paramValue = ctx?.AlarmId ?? "";
                                break;
                            case "priority":
                                paramValue = ctx?.Priority ?? "";
                                break;
                            case "value":
                                paramValue = ctx?.Value ?? "";
                                break;
                            case "username":
                            case "user":
                            case "userid":
                                paramValue = Settings.Username ?? "";
                                break;
                            case "password":
                            case "pass":
                                paramValue = Settings.Password ?? "";
                                break;
                            case "sender_name":
                            case "sendername":
                            case "sender":
                            case "from":
                                paramValue = Settings.SenderName ?? "SCADA";
                                break;
                            default:
                                paramValue = param.Value;
                                break;
                        }
                        requestParams[param.Key] = paramValue;
                    }
                }

                if (!anyPhoneMapped)
                {
                    _logger.LogWarning("No 'phone' parameter mapped in REST ApiParams — recipient {PhoneNumber} may not receive SMS. " +
                        "Add a row in Settings → Body Parameters with the dynamic value `phone`.", MaskPhoneNumber(phoneNumber));
                }

                var customHeaders = new Dictionary<string, string>();
                if (Settings.RestHeaders.Count > 0)
                {
                    foreach (var h in Settings.RestHeaders)
                        if (!string.IsNullOrWhiteSpace(h.Key))
                            customHeaders[h.Key] = h.Value;
                }
                else if (!string.IsNullOrEmpty(Settings.ApiHeaders))
                {
                    try
                    {
                        customHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings.ApiHeaders)
                                        ?? new Dictionary<string, string>();
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning("ApiHeaders is not valid JSON — custom headers ignored");
                    }
                }

                // COMPREHENSIVE SECURITY MASKING FOR DEBUG LOGS
                var logData = CreateSecureLookupForLogging(requestParams);

                _logger.LogDebug("=== UNIVERSAL SMS API CALL DEBUG ===");
                _logger.LogDebug("Endpoint: {Endpoint}", Settings.ApiEndpoint);
                _logger.LogDebug("HTTP Method: {Method}", Settings.HttpMethod);
                _logger.LogDebug("Content-Type: {ContentType}", Settings.ContentType);
                _logger.LogDebug("Parameters (SECURED): {@Parameters}", logData);
                _logger.LogDebug("Total Parameters Count: {Count}", requestParams.Count);

                _logger.LogInformation("Sending SMS API request via {Method} to {Endpoint} with parameters: {@Parameters}",
                    Settings.HttpMethod, Settings.ApiEndpoint, logData);

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;
                var method = Settings.HttpMethod.ToUpper();

                if (method == "GET")
                {
                    var queryParams = requestParams.Select(kvp =>
                        $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
                    var fullUrl = $"{Settings.ApiEndpoint}?{string.Join("&", queryParams)}";
                    _logger.LogDebug("GET URL (SECURED): {FullUrl}", MaskSensitiveDataInUrl(fullUrl));

                    using var getRequest = new HttpRequestMessage(HttpMethod.Get, fullUrl);
                    fullUrl = ApplyRestAuth(getRequest, fullUrl);
                    getRequest.RequestUri = new Uri(fullUrl);
                    AddCustomHeaders(getRequest, customHeaders);
                    response = await _httpClient.SendAsync(getRequest, cancellationToken);
                }
                else if (method == "POST" || method == "PUT" || method == "PATCH")
                {
                    HttpContent content;
                    if (Settings.ContentType.Contains("application/json"))
                    {
                        var jsonObject = requestParams.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
                        var jsonString = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = false });
                        content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                        _logger.LogDebug("JSON Body (SECURED): {JsonBody}", MaskSensitiveDataInJson(jsonString));
                    }
                    else
                    {
                        content = new FormUrlEncodedContent(requestParams);
                        _logger.LogDebug("Form Data (SECURED): {FormData}", CreateSecureFormDataForLogging(requestParams));
                    }

                    var httpMethod = method switch
                    {
                        "POST"  => HttpMethod.Post,
                        "PUT"   => HttpMethod.Put,
                        "PATCH" => HttpMethod.Patch,
                        _       => throw new NotSupportedException($"HTTP method {Settings.HttpMethod} not supported")
                    };

                    using var bodyRequest = new HttpRequestMessage(httpMethod, Settings.ApiEndpoint) { Content = content };
                    var resolvedUrl = ApplyRestAuth(bodyRequest, Settings.ApiEndpoint);
                    if (!ReferenceEquals(resolvedUrl, Settings.ApiEndpoint))
                        bodyRequest.RequestUri = new Uri(resolvedUrl);
                    AddCustomHeaders(bodyRequest, customHeaders);
                    response = await _httpClient.SendAsync(bodyRequest, cancellationToken);
                }
                else
                {
                    throw new NotSupportedException($"HTTP method {Settings.HttpMethod} not supported");
                }

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                sw.Stop();

                // ?? DETAILED RESPONSE DEBUGGING (RESPONSE CONTENT IS SAFE)
                _logger.LogDebug("=== SMS API RESPONSE DEBUG ===");
                _logger.LogDebug("HTTP Status: {StatusCode} ({StatusName})", (int)response.StatusCode, response.StatusCode);
                _logger.LogDebug("Response Headers:");
                foreach (var header in response.Headers)
                {
                    _logger.LogDebug("  {HeaderName}: {HeaderValue}", header.Key, string.Join(", ", header.Value));
                }
                _logger.LogDebug("Response Content Length: {Length}", responseContent?.Length ?? 0);
                _logger.LogDebug("Response Body: {Response}", responseContent);

                SmsApiResponse restResult;

                if (response.IsSuccessStatusCode)
                {
                    // Failure pattern overrides 2xx (provider returns 200 but body signals error)
                    if (!string.IsNullOrEmpty(Settings.RestFailurePattern) &&
                        responseContent.Contains(Settings.RestFailurePattern, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogError("REST failure pattern '{Pattern}' found in response for {PhoneNumber}: {Response}",
                            Settings.RestFailurePattern, MaskPhoneNumber(phoneNumber), responseContent);
                        restResult = new SmsApiResponse { Success = false, Message = "Provider returned failure pattern in response", Response = responseContent, StatusCode = (int)response.StatusCode };
                    }
                    else if (!string.IsNullOrEmpty(Settings.RestSuccessPattern) &&
                        !responseContent.Contains(Settings.RestSuccessPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogError("REST success pattern '{Pattern}' not found in response for {PhoneNumber}: {Response}",
                            Settings.RestSuccessPattern, MaskPhoneNumber(phoneNumber), responseContent);
                        restResult = new SmsApiResponse { Success = false, Message = "Expected success pattern not found in response", Response = responseContent, StatusCode = (int)response.StatusCode };
                    }
                    else
                    {
                        _logger.LogInformation("SMS sent successfully to {PhoneNumber} - HTTP {StatusCode} received via {Method}",
                            MaskPhoneNumber(phoneNumber), response.StatusCode, Settings.HttpMethod);
                        restResult = new SmsApiResponse
                        {
                            Success = true,
                            Message = $"SMS sent successfully - HTTP {response.StatusCode}",
                            Response = responseContent,
                            StatusCode = (int)response.StatusCode
                        };
                    }
                }
                else
                {
                    _logger.LogError("SMS API call failed with status {StatusCode} for {PhoneNumber}: {Response}",
                        response.StatusCode, MaskPhoneNumber(phoneNumber), responseContent);
                    restResult = new SmsApiResponse
                    {
                        Success = false,
                        Message = $"API call failed - HTTP {response.StatusCode}",
                        Response = responseContent,
                        StatusCode = (int)response.StatusCode
                    };
                }

                _callLog.Add(new SmsCallEntry
                {
                    Timestamp      = DateTime.Now,
                    Provider       = "REST",
                    Endpoint       = Settings.ApiEndpoint,
                    HttpMethod     = Settings.HttpMethod,
                    PhoneNumber    = MaskPhoneNumber(phoneNumber),
                    RequestSummary = string.Join(", ", logData.Select(kvp => $"{kvp.Key}={kvp.Value}")),
                    FullResponse   = responseContent,
                    StatusCode     = (int)response.StatusCode,
                    Success        = restResult.Success,
                    DurationMs     = sw.ElapsedMilliseconds
                });

                return restResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making SMS API call to {PhoneNumber} via {Method}", MaskPhoneNumber(phoneNumber), Settings.HttpMethod);
                _callLog.Add(new SmsCallEntry
                {
                    Timestamp   = DateTime.Now,
                    Provider    = "REST",
                    Endpoint    = Settings.ApiEndpoint,
                    HttpMethod  = Settings.HttpMethod,
                    PhoneNumber = MaskPhoneNumber(phoneNumber),
                    FullResponse= ex.Message,
                    Success     = false,
                    StatusCode  = 0
                });
                return new SmsApiResponse
                {
                    Success = false,
                    Message = ex.Message,
                    Response = ex.ToString(),
                    StatusCode = 0
                };
            }
        }

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

        private async Task<SmsApiResponse> SendSoapApiCallAsync(string message, SmsRecipient recipient)
            => await SendSoapApiCallAsync(message, recipient, null);

        private async Task<SmsApiResponse> SendSoapApiCallAsync(string message, SmsRecipient recipient, SmsCallContext? ctx)
            => await SendSoapApiCallAsync(message, recipient, ctx, CancellationToken.None);

        private async Task<SmsApiResponse> SendSoapApiCallAsync(string message, SmsRecipient recipient, SmsCallContext? ctx, CancellationToken cancellationToken)
        {
            try
            {
                var phone = recipient.PhoneNumber;
                var tz = recipient.TZ ?? "0";
                var firstName = recipient.FirstName ?? "User";
                var lastName = recipient.LastName ?? "User";

                // Build SOAP header block based on auth type
                var authType = Settings.SoapAuthType ?? "WSSecurity";

                var wsUser = Settings.SoapPlaceholders
                    .FirstOrDefault(p => p.Key.Equals("Username", StringComparison.OrdinalIgnoreCase))?.Value
                    ?? Settings.Username ?? "";
                var wsPass = Settings.SoapPlaceholders
                    .FirstOrDefault(p => p.Key.Equals("Password", StringComparison.OrdinalIgnoreCase))?.Value
                    ?? Settings.Password ?? "";

                var soapHeader = authType.Equals("WSSecurity", StringComparison.OrdinalIgnoreCase)
                    ? $@"  <soapenv:Header>
    <Security xmlns=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"">
      <UsernameToken>
        <Username>{SecurityElement(wsUser)}</Username>
        <Password Type=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText"">{SecurityElement(wsPass)}</Password>
      </UsernameToken>
    </Security>
  </soapenv:Header>"
                    : "  <soapenv:Header/>";

                // Build SOAP body from configured template (required — no client-specific defaults)
                if (string.IsNullOrWhiteSpace(Settings.SoapBodyTemplate))
                    throw new InvalidOperationException(
                        "SOAP Body Template is required. Configure it in Settings → Provider → SOAP.");

                // Resolve a SOAP dynamic keyword to its runtime value.
                string ResolveSoapValue(string keyword) => keyword.ToLowerInvariant() switch
                {
                    "message"                              => SecurityElement(message),
                    "phone" or "phonenumber" or "mobile"  => SecurityElement(phone),
                    "tz"                                   => SecurityElement(tz),
                    "username" or "user"                   => SecurityElement(Settings.Username ?? ""),
                    "password" or "pass"                   => SecurityElement(Settings.Password ?? ""),
                    "sender_name" or "sendername" or "from"=> SecurityElement(Settings.SenderName ?? ""),
                    "firstname"                            => SecurityElement(firstName),
                    "lastname"                             => SecurityElement(lastName),
                    "sendingsystem"                        => SecurityElement(Settings.SoapSendingSystem ?? "SCADA"),
                    "messagetype"                          => SecurityElement(Settings.SoapMessageType ?? "SmsType1"),
                    "group_id" or "groupid"                => SecurityElement(ctx?.GroupId?.ToString() ?? ""),
                    "alarm_id" or "alarmid"                => SecurityElement(ctx?.AlarmId ?? ""),
                    "priority"                             => SecurityElement(ctx?.Priority ?? ""),
                    "value"                                => SecurityElement(ctx?.Value ?? ""),
                    _                                      => SecurityElement(keyword) // literal static value
                };

                var soapBodyContent = Settings.SoapBodyTemplate;

                var phoneKws = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "phone", "phonenumber", "mobile" };
                var messageKws = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "message", "msg", "text", "body" };

                if (Settings.SoapPlaceholders.Count > 0)
                {
                    // New dynamic list path
                    ValidateRequiredSoapMapping(soapBodyContent, Settings.SoapPlaceholders, phoneKws, "phone");
                    ValidateRequiredSoapMapping(soapBodyContent, Settings.SoapPlaceholders, messageKws, "message");
                    foreach (var param in Settings.SoapPlaceholders)
                        soapBodyContent = soapBodyContent.Replace($"{{{param.Key}}}", ResolveSoapValue(param.Value));
                }
                else if (!string.IsNullOrWhiteSpace(Settings.SoapParams))
                {
                    // Legacy SoapParams JSON string
                    var soapParamMap = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings.SoapParams)
                                       ?? new Dictionary<string, string>();
                    var legacyParams = soapParamMap.Select(p => new SmsParam { Key = p.Key, Value = p.Value }).ToList();
                    ValidateRequiredSoapMapping(soapBodyContent, legacyParams, phoneKws, "phone");
                    ValidateRequiredSoapMapping(soapBodyContent, legacyParams, messageKws, "message");
                    foreach (var (token, value) in soapParamMap)
                        soapBodyContent = soapBodyContent.Replace($"{{{token}}}", ResolveSoapValue(value));
                }
                else
                {
                    RequireSingleToken(soapBodyContent, "Phone", "phone");
                    RequireSingleToken(soapBodyContent, "Message", "message");

                    // Legacy hardcoded replacements (backward-compatible)
                    soapBodyContent = soapBodyContent
                        .Replace("{Message}",       SecurityElement(message))
                        .Replace("{Phone}",         SecurityElement(phone))
                        .Replace("{Username}",      SecurityElement(Settings.Username ?? ""))
                        .Replace("{Password}",      SecurityElement(Settings.Password ?? ""))
                        .Replace("{SenderName}",    SecurityElement(Settings.SenderName ?? ""))
                        .Replace("{FirstName}",     SecurityElement(firstName))
                        .Replace("{LastName}",      SecurityElement(lastName))
                        .Replace("{TZ}",            SecurityElement(tz))
                        .Replace("{SendingSystem}", SecurityElement(Settings.SoapSendingSystem ?? "SCADA"))
                        .Replace("{MessageType}",   SecurityElement(Settings.SoapMessageType ?? "SmsType1"));
                }

                if (System.Text.RegularExpressions.Regex.IsMatch(soapBodyContent, @"\{[^{}]+\}"))
                    throw new InvalidOperationException("SOAP Body Template contains unresolved placeholders.");

                // Extra namespace declarations (e.g. xmlns:ns1="http://myprovider.com")
                var extraNs = string.IsNullOrWhiteSpace(Settings.SoapEnvelopeNamespaces)
                    ? ""
                    : " " + Settings.SoapEnvelopeNamespaces.Trim();

                var soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/""{extraNs}>
{soapHeader}
  <soapenv:Body>
{soapBodyContent}
  </soapenv:Body>
</soapenv:Envelope>";

                ParseXmlSecurely(soapEnvelope, "SOAP request");

                if (Settings.LogSoapXml)
                {
                    var logEnvelope = soapEnvelope;
                    // Redact WS-Security / HttpBasic password
                    if (!string.IsNullOrEmpty(wsPass))
                    {
                        logEnvelope = logEnvelope
                            .Replace(SecurityElement(wsPass), "***", StringComparison.Ordinal)
                            .Replace(wsPass, "***", StringComparison.Ordinal);
                    }
                    // Redact any SOAP placeholder values marked IsSecret
                    foreach (var p in Settings.SoapPlaceholders.Where(p => p.IsSecret && !string.IsNullOrEmpty(p.Value)))
                    {
                        logEnvelope = logEnvelope
                            .Replace(SecurityElement(p.Value), "***", StringComparison.Ordinal)
                            .Replace(p.Value, "***", StringComparison.Ordinal);
                    }
                    _soapXmlLog.Push(MaskPhoneNumber(phone), logEnvelope);
                }

                _logger.LogDebug("Sending SOAP SMS to {PhoneNumber} via {Endpoint} (auth={AuthType})",
                    MaskPhoneNumber(phone), Settings.ApiEndpoint, authType);

                using var request = new HttpRequestMessage(HttpMethod.Post, Settings.ApiEndpoint);
                request.Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{Settings.SoapAction}\"");

                // HTTP Basic auth header (alternative to WS-Security)
                if (authType.Equals("HttpBasic", StringComparison.OrdinalIgnoreCase))
                {
                    var credentials = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes($"{wsUser}:{wsPass}"));
                    request.Headers.TryAddWithoutValidation("Authorization", $"Basic {credentials}");
                }

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                sw.Stop();

                _logger.LogDebug("SOAP response HTTP {StatusCode}: {Body}", (int)response.StatusCode, responseContent);

                SmsApiResponse soapResult;

                if (response.IsSuccessStatusCode)
                {
                    var successPattern = Settings.SoapSuccessPattern?.Trim();
                    if (string.IsNullOrWhiteSpace(successPattern))
                        throw new InvalidOperationException("SOAP Success Pattern is required for provider acceptance confirmation.");

                    var acceptance = EvaluateSoapResponse(
                        responseContent, successPattern, Settings.SoapErrorPattern);
                    var success = acceptance.Success;
                    var errorMessage = acceptance.ErrorMessage;

                    if (success)
                    {
                        _logger.LogInformation("SOAP SMS sent successfully to {PhoneNumber}", MaskPhoneNumber(phone));
                        soapResult = new SmsApiResponse { Success = true, Message = "SMS sent via SOAP", Response = responseContent, StatusCode = (int)response.StatusCode };
                    }
                    else
                    {
                        _logger.LogError("SOAP SMS failed for {PhoneNumber}: {Error}", MaskPhoneNumber(phone), errorMessage);
                        soapResult = new SmsApiResponse
                        {
                            Success = false,
                            Message = string.IsNullOrWhiteSpace(errorMessage)
                                ? "SOAP provider response did not confirm acceptance"
                                : $"SOAP provider error: {errorMessage}",
                            Response = responseContent,
                            StatusCode = (int)response.StatusCode
                        };
                    }
                }
                else
                {
                    _logger.LogError("SOAP SMS HTTP error {StatusCode} for {PhoneNumber}", response.StatusCode, MaskPhoneNumber(phone));
                    soapResult = new SmsApiResponse { Success = false, Message = $"HTTP {response.StatusCode}", Response = responseContent, StatusCode = (int)response.StatusCode };
                }

                _callLog.Add(new SmsCallEntry
                {
                    Timestamp     = DateTime.Now,
                    Provider      = "SOAP",
                    Endpoint      = Settings.ApiEndpoint,
                    HttpMethod    = "POST",
                    PhoneNumber   = MaskPhoneNumber(phone),
                    AlarmId       = ctx?.AlarmId ?? string.Empty,
                    RequestSummary= $"SOAPAction={Settings.SoapAction}",
                    FullResponse  = responseContent,
                    StatusCode    = (int)response.StatusCode,
                    Success       = soapResult.Success,
                    DurationMs    = sw.ElapsedMilliseconds
                });

                return soapResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SOAP SMS to {PhoneNumber}", MaskPhoneNumber(recipient.PhoneNumber));
                _callLog.Add(new SmsCallEntry
                {
                    Timestamp     = DateTime.Now,
                    Provider      = "SOAP",
                    Endpoint      = Settings.ApiEndpoint,
                    HttpMethod    = "POST",
                    PhoneNumber   = MaskPhoneNumber(recipient.PhoneNumber),
                    FullResponse  = ex.Message,
                    Success       = false,
                    StatusCode    = 0
                });
                return new SmsApiResponse { Success = false, Message = ex.Message, Response = ex.ToString(), StatusCode = 0 };
            }
        }

        private static XDocument ParseXmlSecurely(string xml, string description)
        {
            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                };
                using var stringReader = new StringReader(xml);
                using var xmlReader = XmlReader.Create(stringReader, settings);
                return XDocument.Load(xmlReader, LoadOptions.None);
            }
            catch (XmlException ex)
            {
                throw new InvalidOperationException($"{description} is not valid XML: {ex.Message}", ex);
            }
        }

        internal static SoapAcceptanceResult EvaluateSoapResponse(
            string responseContent,
            string successPattern,
            string? errorPattern)
        {
            if (string.IsNullOrWhiteSpace(successPattern))
                throw new InvalidOperationException("SOAP Success Pattern is required for provider acceptance confirmation.");

            var responseXml = ParseXmlSecurely(responseContent, "SOAP response");
            var fault = responseXml.Descendants().FirstOrDefault(e =>
                e.Name.LocalName.Equals("Fault", StringComparison.OrdinalIgnoreCase));
            if (fault != null)
                return new SoapAcceptanceResult(false, ExtractSoapFault(fault));

            var configuredError = FindSoapError(responseXml, errorPattern);
            if (configuredError.Found)
                return new SoapAcceptanceResult(false, configuredError.Message);

            return MatchesSoapPattern(responseXml, responseContent, successPattern)
                ? new SoapAcceptanceResult(true, string.Empty)
                : new SoapAcceptanceResult(false, "SOAP provider response did not confirm acceptance");
        }

        private static bool MatchesSoapPattern(XDocument document, string rawXml, string pattern)
        {
            if (pattern.StartsWith('<'))
            {
                XElement expected;
                try
                {
                    expected = XElement.Parse(pattern);
                }
                catch (XmlException ex)
                {
                    throw new InvalidOperationException(
                        "SOAP Success Pattern must be valid XML. Namespace prefixes require xmlns declarations.", ex);
                }

                var matches = document.Descendants().Where(actual =>
                    actual.Name == expected.Name &&
                    actual.Value.Trim().Equals(expected.Value.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    expected.Attributes().Where(a => !a.IsNamespaceDeclaration).All(expectedAttribute =>
                        actual.Attributes().Any(actualAttribute =>
                            !actualAttribute.IsNamespaceDeclaration &&
                            actualAttribute.Name == expectedAttribute.Name &&
                            actualAttribute.Value.Equals(expectedAttribute.Value, StringComparison.OrdinalIgnoreCase))));

                // Ambiguous duplicate signals are not sufficient proof of provider acceptance.
                return matches.Take(2).Count() == 1;
            }

            return rawXml.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractSoapFault(XElement fault)
        {
            return fault.Descendants().FirstOrDefault(e =>
                       e.Name.LocalName is "faultstring" or "Text" or "Reason")?.Value.Trim()
                   ?? fault.Value.Trim();
        }

        private static (bool Found, string Message) FindSoapError(XDocument document, string? configuredPattern)
        {
            if (string.IsNullOrWhiteSpace(configuredPattern))
                return (false, string.Empty);

            var localName = configuredPattern.Trim().TrimStart('<').TrimEnd('>');
            var separator = localName.IndexOfAny([' ', '/', '>']);
            if (separator >= 0)
                localName = localName[..separator];
            if (localName.Contains(':'))
                localName = localName[(localName.IndexOf(':') + 1)..];

            var element = document.Descendants().FirstOrDefault(e =>
                e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));
            if (element == null)
                return (false, string.Empty);

            var message = element.Value.Trim();
            var hasDetails = message.Length > 0 || element.Attributes().Any(a => !a.IsNamespaceDeclaration);
            return (hasDetails, message);
        }

        internal static void ValidateRequiredSoapMapping(
            string template,
            IEnumerable<SmsParam> mappings,
            HashSet<string> acceptedKeywords,
            string fieldName)
        {
            var mapping = mappings.SingleOrDefault(p => acceptedKeywords.Contains(p.Value ?? string.Empty));
            if (mapping == null)
                throw new InvalidOperationException($"SOAP configuration must map one placeholder to '{fieldName}'.");

            RequireSingleToken(template, mapping.Key, fieldName);
        }

        internal static void RequireSingleToken(string template, string tokenName, string fieldName)
        {
            var token = $"{{{tokenName}}}";
            var count = Regex.Matches(template, Regex.Escape(token), RegexOptions.CultureInvariant).Count;
            if (count != 1)
                throw new InvalidOperationException(
                    $"SOAP Body Template must contain exactly one {token} token for '{fieldName}'.");
        }

        private static readonly HashSet<string> _sensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
            { "authorization", "x-api-key", "x-auth-token", "x-secret", "x-api-secret", "api-key", "token", "x-token" };

        private void AddCustomHeaders(HttpRequestMessage request, Dictionary<string, string> headers)
        {
            foreach (var (name, value) in headers)
            {
                try
                {
                    request.Headers.TryAddWithoutValidation(name, value);
                    var logValue = _sensitiveHeaderNames.Any(s => name.StartsWith(s, StringComparison.OrdinalIgnoreCase))
                        ? "***MASKED***"
                        : value;
                    _logger.LogDebug("Added custom header: {Name}={Value}", name, logValue);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not add custom header {Name}", name);
                }
            }
        }

        /// <summary>
        /// Applies REST authentication to the outgoing request based on Settings.RestAuthType.
        /// For ApiKey-in-Query the caller must use the returned URL instead of the original endpoint.
        /// </summary>
        private string ApplyRestAuth(HttpRequestMessage request, string url)
        {
            var authType = (Settings.RestAuthType ?? "None").Trim();
            if (string.IsNullOrEmpty(authType) || authType.Equals("None", StringComparison.OrdinalIgnoreCase))
                return url;

            switch (authType.ToLowerInvariant())
            {
                case "basic":
                    if (!string.IsNullOrEmpty(Settings.Username))
                    {
                        var raw = $"{Settings.Username}:{Settings.Password}";
                        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                        request.Headers.TryAddWithoutValidation("Authorization", $"Basic {b64}");
                        _logger.LogDebug("Applied REST Basic auth header (Authorization: Basic ***)");
                    }
                    return url;

                case "bearer":
                    if (!string.IsNullOrEmpty(Settings.RestBearerToken))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Settings.RestBearerToken}");
                        _logger.LogDebug("Applied REST Bearer token (Authorization: Bearer ***)");
                    }
                    return url;

                case "apikey":
                    if (string.IsNullOrEmpty(Settings.RestApiKeyName))
                        return url;
                    var location = (Settings.RestApiKeyLocation ?? "Header").Trim();
                    if (location.Equals("Query", StringComparison.OrdinalIgnoreCase))
                    {
                        var sep = url.Contains('?') ? "&" : "?";
                        var newUrl = $"{url}{sep}{Uri.EscapeDataString(Settings.RestApiKeyName)}={Uri.EscapeDataString(Settings.RestApiKeyValue ?? string.Empty)}";
                        _logger.LogDebug("Applied REST API key as query parameter (***)");
                        return newUrl;
                    }
                    request.Headers.TryAddWithoutValidation(Settings.RestApiKeyName, Settings.RestApiKeyValue ?? string.Empty);
                    _logger.LogDebug("Applied REST API key header {HeaderName}=***", Settings.RestApiKeyName);
                    return url;

                default:
                    _logger.LogWarning("Unknown RestAuthType '{AuthType}' - no auth applied", authType);
                    return url;
            }
        }

        private static string SecurityElement(string value) =>
            System.Security.SecurityElement.Escape(value) ?? value;

        // ?? ENHANCED SECURITY HELPER METHODS
        private Dictionary<string, string> CreateSecureLookupForLogging(Dictionary<string, string> requestParams)
        {
            var secureParams = new Dictionary<string, string>();
            
            foreach (var param in requestParams)
            {
                if (IsSensitiveParameter(param.Key))
                {
                    secureParams[param.Key] = "***MASKED***";
                }
                else if (param.Key.ToLower().Contains("phone") || param.Key.ToLower().Contains("sendtophonenumbers"))
                {
                    secureParams[param.Key] = MaskPhoneNumber(param.Value);
                }
                else
                {
                    secureParams[param.Key] = param.Value;
                }
            }
            
            return secureParams;
        }

        private string MaskSensitiveDataInUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            
            var maskedUrl = url;
            
            // Mask password parameter
            if (!string.IsNullOrEmpty(Settings.Password))
            {
                maskedUrl = maskedUrl.Replace(Settings.Password, "***PASSWORD***");
                maskedUrl = maskedUrl.Replace(Uri.EscapeDataString(Settings.Password), "***PASSWORD***");
            }
            
            // Mask phone numbers (look for patterns like phone number)
            maskedUrl = System.Text.RegularExpressions.Regex.Replace(
                maskedUrl, 
                @"(\+?[0-9]{10,15})", 
                match => MaskPhoneNumber(Uri.UnescapeDataString(match.Value))
            );
            
            return maskedUrl;
        }

        private string MaskSensitiveDataInJson(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString)) return jsonString;
            
            var maskedJson = jsonString;
            
            // Mask password in JSON
            if (!string.IsNullOrEmpty(Settings.Password))
            {
                maskedJson = maskedJson.Replace(Settings.Password, "***PASSWORD***");
            }
            
            return maskedJson;
        }

        private string CreateSecureFormDataForLogging(Dictionary<string, string> requestParams)
        {
            return string.Join("&", 
                requestParams.Where(kvp => !IsSensitiveParameter(kvp.Key))
                           .Select(kvp => 
                           {
                               var value = kvp.Key.ToLower().Contains("phone") || kvp.Key.ToLower().Contains("sendtophonenumbers")
                                   ? MaskPhoneNumber(kvp.Value)
                                   : kvp.Value;
                               return $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(value)}";
                           }));
        }

        private bool IsSensitiveParameter(string paramName)
        {
            var lowerParam = paramName.ToLower();
            return lowerParam.Contains("password") || 
                   lowerParam.Contains("pass") ||
                   lowerParam.Contains("key") ||
                   lowerParam.Contains("secret") ||
                   lowerParam.Contains("token");
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

        private string ResolveRestKeyword(string keyword, string phoneNumber, SmsRecipient recipient, SmsCallContext? ctx)
        {
            switch (keyword.ToLowerInvariant())
            {
                case "phone": case "phonenumber": case "sendtophonenumbers": case "mobile": case "number":
                    return phoneNumber;
                case "tz":
                    return recipient.TZ ?? "";
                case "firstname":
                    return recipient.FirstName ?? "";
                case "lastname":
                    return recipient.LastName ?? "";
                case "group_id": case "groupid":
                    return ctx?.GroupId?.ToString() ?? "";
                case "alarm_id": case "alarmid":
                    return ctx?.AlarmId ?? "";
                case "priority":
                    return ctx?.Priority ?? "";
                case "value":
                    return ctx?.Value ?? "";
                case "username": case "user": case "userid":
                    return Settings.Username ?? "";
                case "password": case "pass":
                    return Settings.Password ?? "";
                case "sender_name": case "sendername": case "sender": case "from":
                    return Settings.SenderName ?? "SCADA";
                default:
                    return keyword; // literal value
            }
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
    public sealed record SmsCallContext(int? GroupId, string? AlarmId, string? Priority, string? Value);
    public sealed record SoapAcceptanceResult(bool Success, string ErrorMessage);
}
