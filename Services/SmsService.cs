using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Models;
using System.Text;
using System.Text.Json;

namespace SCADASMSSystem.Web.Services
{
    public class SmsService : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly SmsSettings _smsSettings;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SmsService> _logger;
        private static readonly Dictionary<string, DateTime> _lastMessageTimes = new();
        private static readonly Dictionary<string, int> _rateLimitCounter = new();
        private static DateTime _lastRateLimitReset = DateTime.Now;

        public SmsService(
            HttpClient httpClient,
            IOptions<SmsSettings> smsSettings,
            IServiceProvider serviceProvider,
            ILogger<SmsService> logger)
        {
            _httpClient = httpClient;
            _smsSettings = smsSettings.Value;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<bool> SendSmsAsync(string message, IEnumerable<string> phoneNumbers, string alarmId, int? groupId = null)
        {
            try
            {
                var success = true;
                var tasks = new List<Task>();

                foreach (var phoneNumber in phoneNumbers)
                {
                    // Check rate limiting
                    if (!CheckRateLimit())
                    {
                        _logger.LogWarning("Rate limit exceeded, skipping SMS to {PhoneNumber}", MaskPhoneNumber(phoneNumber));
                        continue;
                    }

                    // Check duplicate prevention
                    if (IsDuplicate(message, phoneNumber))
                    {
                        _logger.LogInformation("Duplicate message blocked for {PhoneNumber}", MaskPhoneNumber(phoneNumber));
                        continue;
                    }

                    tasks.Add(SendSingleSmsAsync(message, phoneNumber, alarmId, groupId));
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

                var recipients = await userService.GetSmsRecipientsForGroupAsync(groupId);
                var phoneNumbers = recipients.Select(r => r.PhoneNumber).ToList();

                if (!phoneNumbers.Any())
                {
                    _logger.LogWarning("No SMS recipients found for group {GroupId}", groupId);
                    return false;
                }

                _logger.LogInformation("Sending SMS to {Count} recipients in group {GroupId}", phoneNumbers.Count, groupId);
                return await SendSmsAsync(message, phoneNumbers, alarmId, groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending SMS to group {GroupId} for alarm {AlarmId}", groupId, alarmId);
                return false;
            }
        }

        public async Task<SmsApiResponse> SendSmsApiCallAsync(string message, string phoneNumber)
        {
            try
            {
                // Parse API parameters from JSON
                var apiParams = JsonSerializer.Deserialize<Dictionary<string, string>>(_smsSettings.ApiParams);
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
                            paramValue = _smsSettings.Username ?? "";
                            break;
                        case "password":
                        case "pass":
                            paramValue = _smsSettings.Password ?? "";
                            break;
                        case "sender_name":
                        case "sendername":
                        case "sender":
                        case "from":
                            paramValue = _smsSettings.SenderName ?? "SCADA";
                            break;
                        default:
                            paramValue = param.Value;
                            break;
                    }
                    
                    requestParams[param.Key] = paramValue;
                }

                // ?? COMPREHENSIVE SECURITY MASKING FOR DEBUG LOGS
                var logData = CreateSecureLookupForLogging(requestParams);
                
                _logger.LogDebug("=== UNIVERSAL SMS API CALL DEBUG ===");
                _logger.LogDebug("Endpoint: {Endpoint}", _smsSettings.ApiEndpoint);
                _logger.LogDebug("HTTP Method: {Method}", _smsSettings.HttpMethod);
                _logger.LogDebug("Content-Type: {ContentType}", _smsSettings.ContentType);
                _logger.LogDebug("Parameters (SECURED): {@Parameters}", logData);
                _logger.LogDebug("Total Parameters Count: {Count}", requestParams.Count);

                _logger.LogInformation("Sending SMS API request via {Method} to {Endpoint} with parameters: {@Parameters}", 
                    _smsSettings.HttpMethod, _smsSettings.ApiEndpoint, logData);

                HttpResponseMessage response;

                // Handle different HTTP methods and content types
                switch (_smsSettings.HttpMethod.ToUpper())
                {
                    case "GET":
                        // For GET requests, append parameters to URL
                        var queryParams = requestParams.Select(kvp => 
                            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
                        var fullUrl = $"{_smsSettings.ApiEndpoint}?{string.Join("&", queryParams)}";
                        
                        // ENHANCED SECURITY: Mask ALL sensitive data in URL
                        var secureUrl = MaskSensitiveDataInUrl(fullUrl);
                        _logger.LogDebug("GET URL (SECURED): {FullUrl}", secureUrl);
                        response = await _httpClient.GetAsync(fullUrl);
                        break;

                    case "POST":
                    case "PUT":
                    case "PATCH":
                        // Determine content based on ContentType setting
                        HttpContent content;
                        
                        if (_smsSettings.ContentType.Contains("application/json"))
                        {
                            // JSON content
                            var jsonObject = requestParams.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value);
                            var jsonString = JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = false });
                            content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                            
                            // ENHANCED SECURITY: Mask ALL sensitive JSON data
                            var secureJson = MaskSensitiveDataInJson(jsonString);
                            _logger.LogDebug("JSON Body (SECURED): {JsonBody}", secureJson);
                        }
                        else
                        {
                            // Form URL encoded content (default)
                            content = new FormUrlEncodedContent(requestParams);
                            
                            // ENHANCED SECURITY: Only log non-sensitive form data
                            var secureFormData = CreateSecureFormDataForLogging(requestParams);
                            _logger.LogDebug("Form Data (SECURED): {FormData}", secureFormData);
                        }

                        // Send appropriate HTTP method
                        if (_smsSettings.HttpMethod.ToUpper() == "POST")
                            response = await _httpClient.PostAsync(_smsSettings.ApiEndpoint, content);
                        else if (_smsSettings.HttpMethod.ToUpper() == "PUT")
                            response = await _httpClient.PutAsync(_smsSettings.ApiEndpoint, content);
                        else if (_smsSettings.HttpMethod.ToUpper() == "PATCH")
                            response = await _httpClient.PatchAsync(_smsSettings.ApiEndpoint, content);
                        else
                            throw new NotSupportedException($"HTTP method {_smsSettings.HttpMethod} not supported");
                        break;

                    default:
                        throw new NotSupportedException($"HTTP method {_smsSettings.HttpMethod} not supported");
                }

                var responseContent = await response.Content.ReadAsStringAsync();

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

                // Accept all 2xx status codes as success
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("SMS sent successfully to {PhoneNumber} - HTTP {StatusCode} received via {Method}", 
                        MaskPhoneNumber(phoneNumber), response.StatusCode, _smsSettings.HttpMethod);
                    return new SmsApiResponse 
                    { 
                        Success = true, 
                        Message = $"SMS sent successfully - HTTP {response.StatusCode}",
                        Response = responseContent,
                        StatusCode = (int)response.StatusCode
                    };
                }
                else
                {
                    _logger.LogError("SMS API call failed with status {StatusCode} for {PhoneNumber}: {Response}", 
                        response.StatusCode, MaskPhoneNumber(phoneNumber), responseContent);

                    return new SmsApiResponse 
                    { 
                        Success = false, 
                        Message = $"API call failed - HTTP {response.StatusCode}",
                        Response = responseContent,
                        StatusCode = (int)response.StatusCode
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making SMS API call to {PhoneNumber} via {Method}", MaskPhoneNumber(phoneNumber), _smsSettings.HttpMethod);
                return new SmsApiResponse 
                { 
                    Success = false, 
                    Message = ex.Message,
                    Response = ex.ToString(),
                    StatusCode = 0
                };
            }
        }

        private async Task SendSingleSmsAsync(string message, string phoneNumber, string alarmId, int? groupId = null)
        {
            try
            {
                var response = await SendSmsApiCallAsync(message, phoneNumber);
                
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
            if (!string.IsNullOrEmpty(_smsSettings.Password))
            {
                maskedUrl = maskedUrl.Replace(_smsSettings.Password, "***PASSWORD***");
                maskedUrl = maskedUrl.Replace(Uri.EscapeDataString(_smsSettings.Password), "***PASSWORD***");
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
            if (!string.IsNullOrEmpty(_smsSettings.Password))
            {
                maskedJson = maskedJson.Replace(_smsSettings.Password, "***PASSWORD***");
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
            if ((now - _lastRateLimitReset).TotalSeconds >= _smsSettings.RateWindow)
            {
                _rateLimitCounter.Clear();
                _lastRateLimitReset = now;
            }

            // Check current count
            var currentMinute = now.ToString("yyyy-MM-dd HH:mm");
            _rateLimitCounter.TryGetValue(currentMinute, out int currentCount);
            
            if (currentCount >= _smsSettings.RateLimit)
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
                if (minutesSinceLastMessage < _smsSettings.DuplicateWindow)
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
            var cutoffTime = DateTime.Now.AddMinutes(-_smsSettings.DuplicateWindow);
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