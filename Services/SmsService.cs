using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SCADASMSSystem.Web.Services
{
    public class SmsService : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly IOptionsMonitor<SmsSettings> _smsMonitor;
        private readonly IServiceProvider _serviceProvider;
        private readonly SmsCallLog _callLog;
        private readonly ILogger<SmsService> _logger;
        private static readonly Dictionary<string, DateTime> _lastMessageTimes = new();
        private static readonly Dictionary<string, int> _rateLimitCounter = new();
        private static DateTime _lastRateLimitReset = DateTime.Now;

        private SmsSettings Settings => _smsMonitor.CurrentValue;

        public SmsService(
            HttpClient httpClient,
            IOptionsMonitor<SmsSettings> smsSettings,
            IServiceProvider serviceProvider,
            SmsCallLog callLog,
            ILogger<SmsService> logger)
        {
            _httpClient = httpClient;
            _smsMonitor = smsSettings;
            _serviceProvider = serviceProvider;
            _callLog = callLog;
            _logger = logger;

            var timeout = Settings.TimeoutSeconds > 0 ? Settings.TimeoutSeconds : 30;
            _httpClient.Timeout = TimeSpan.FromSeconds(timeout);
        }

        public async Task<bool> SendSmsAsync(string message, IEnumerable<SmsRecipient> recipients, string alarmId, int? groupId = null)
        {
            try
            {
                var success = true;
                var tasks = new List<Task>();

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

                    tasks.Add(SendSingleSmsAsync(message, recipient, alarmId, groupId));
                }

                await Task.WhenAll(tasks);
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SMS for alarm {AlarmId}", alarmId);
                return false;
            }
        }

        public async Task<bool> SendSmsToGroupAsync(string message, int groupId, string alarmId)
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
                return await SendSmsAsync(message, smsRecipients, alarmId, groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SMS to group {GroupId} for alarm {AlarmId}", groupId, alarmId);
                return false;
            }
        }

        public async Task<SmsApiResponse> SendSmsApiCallAsync(string message, SmsRecipient recipient)
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
                return await SendSoapApiCallAsync(message, recipient);

            var phoneNumber = recipient.PhoneNumber;

            try
            {
                // Parse API parameters from JSON
                var apiParams = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings.ApiParams);
                if (apiParams == null)
                {
                    return new SmsApiResponse { Success = false, Message = "Invalid API parameters configuration" };
                }

                // Build parameter dictionary
                var requestParams = new Dictionary<string, string>();
                
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

                // Parse custom request headers
                var customHeaders = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(Settings.ApiHeaders))
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
                    AddCustomHeaders(getRequest, customHeaders);
                    response = await _httpClient.SendAsync(getRequest);
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
                    AddCustomHeaders(bodyRequest, customHeaders);
                    response = await _httpClient.SendAsync(bodyRequest);
                }
                else
                {
                    throw new NotSupportedException($"HTTP method {Settings.HttpMethod} not supported");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
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

        private async Task SendSingleSmsAsync(string message, SmsRecipient recipient, string alarmId, int? groupId = null)
        {
            var phoneNumber = recipient.PhoneNumber;
            try
            {
                var response = await SendSmsApiCallAsync(message, recipient);
                
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FATAL ERROR in SendSingleSmsAsync for {PhoneNumber}, alarm {AlarmId}", MaskPhoneNumber(phoneNumber), alarmId);
                throw; // Re-throw to ensure background service counts this as failed
            }
        }

        private async Task<SmsApiResponse> SendSoapApiCallAsync(string message, SmsRecipient recipient)
        {
            try
            {
                var phone = recipient.PhoneNumber;
                var tz = recipient.TZ ?? "0";
                var firstName = recipient.FirstName ?? "User";
                var lastName = recipient.LastName ?? "User";

                // Build SOAP header block based on auth type
                var authType = Settings.SoapAuthType ?? "WSSecurity";
                var soapHeader = authType.Equals("WSSecurity", StringComparison.OrdinalIgnoreCase)
                    ? $@"  <soapenv:Header>
    <Security xmlns=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"">
      <UsernameToken>
        <Username>{SecurityElement(Settings.Username ?? "")}</Username>
        <Password Type=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText"">{SecurityElement(Settings.Password ?? "")}</Password>
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
                    _                                      => SecurityElement(keyword) // literal static value
                };

                var soapBodyContent = Settings.SoapBodyTemplate;

                if (!string.IsNullOrWhiteSpace(Settings.SoapParams))
                {
                    // Dynamic KV-table resolution: SoapParams JSON maps placeholder → keyword/value
                    var soapParamMap = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings.SoapParams)
                                       ?? new Dictionary<string, string>();
                    foreach (var (token, value) in soapParamMap)
                        soapBodyContent = soapBodyContent.Replace($"{{{token}}}", ResolveSoapValue(value));
                }
                else
                {
                    // Legacy hardcoded replacements (backward-compatible when SoapParams not configured)
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

                _logger.LogDebug("Sending SOAP SMS to {PhoneNumber} via {Endpoint} (auth={AuthType})",
                    MaskPhoneNumber(phone), Settings.ApiEndpoint, authType);

                using var request = new HttpRequestMessage(HttpMethod.Post, Settings.ApiEndpoint);
                request.Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                request.Headers.Add("SOAPAction", $"\"{Settings.SoapAction}\"");

                // HTTP Basic auth header (alternative to WS-Security)
                if (authType.Equals("HttpBasic", StringComparison.OrdinalIgnoreCase))
                {
                    var credentials = Convert.ToBase64String(
                        Encoding.UTF8.GetBytes($"{Settings.Username}:{Settings.Password}"));
                    request.Headers.TryAddWithoutValidation("Authorization", $"Basic {credentials}");
                }

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                sw.Stop();

                _logger.LogDebug("SOAP response HTTP {StatusCode}: {Body}", (int)response.StatusCode, responseContent);

                SmsApiResponse soapResult;

                if (response.IsSuccessStatusCode)
                {
                    var successPattern = Settings.SoapSuccessPattern;
                    var success = responseContent.Contains(successPattern, StringComparison.OrdinalIgnoreCase);
                    var errorMessage = string.Empty;

                    if (!success)
                    {
                        var errorPattern = Settings.SoapErrorPattern;
                        var closeTag = errorPattern.TrimStart('<').TrimEnd('>');
                        var errorStart = responseContent.IndexOf(errorPattern, StringComparison.OrdinalIgnoreCase);
                        var errorEnd = responseContent.IndexOf($"</{closeTag}>", StringComparison.OrdinalIgnoreCase);
                        if (errorStart >= 0 && errorEnd > errorStart)
                            errorMessage = responseContent.Substring(errorStart + errorPattern.Length, errorEnd - errorStart - errorPattern.Length);
                    }

                    if (success)
                    {
                        _logger.LogInformation("SOAP SMS sent successfully to {PhoneNumber}", MaskPhoneNumber(phone));
                        soapResult = new SmsApiResponse { Success = true, Message = "SMS sent via SOAP", Response = responseContent, StatusCode = (int)response.StatusCode };
                    }
                    else
                    {
                        _logger.LogError("SOAP SMS failed for {PhoneNumber}: {Error}", MaskPhoneNumber(phone), errorMessage);
                        soapResult = new SmsApiResponse { Success = false, Message = $"SOAP provider error: {errorMessage}", Response = responseContent, StatusCode = (int)response.StatusCode };
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
                    AlarmId       = string.Empty,
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

        private bool CheckRateLimit()
        {
            var now = DateTime.Now;
            
            // Reset rate limit counter if window has passed
            if ((now - _lastRateLimitReset).TotalSeconds >= Settings.RateWindow)
            {
                _rateLimitCounter.Clear();
                _lastRateLimitReset = now;
            }

            // Check current count
            var currentMinute = now.ToString("yyyy-MM-dd HH:mm");
            _rateLimitCounter.TryGetValue(currentMinute, out int currentCount);
            
            if (currentCount >= Settings.RateLimit)
            {
                return false;
            }

            // Increment counter
            _rateLimitCounter[currentMinute] = currentCount + 1;
            return true;
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
                _lastMessageTimes.Remove(keyToRemove);
            }
        }
    }
}