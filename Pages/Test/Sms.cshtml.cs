using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SCADASMSSystem.Web.Pages.Test
{
    public class SmsModel : PageModel
    {
        private readonly SmsBackgroundService _smsBackgroundService;
        private readonly IGroupService _groupService;
        private readonly ILogger<SmsModel> _logger;
        private readonly IOptionsMonitor<SmsSettings> _smsMonitor;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;

        private SmsSettings Settings => _smsMonitor.CurrentValue;

        public SmsModel(
            SmsBackgroundService smsBackgroundService,
            IGroupService groupService,
            ILogger<SmsModel> logger,
            IOptionsMonitor<SmsSettings> smsSettings,
            IHttpClientFactory httpClientFactory,
            IWebHostEnvironment env,
            IConfiguration configuration)
        {
            _smsBackgroundService = smsBackgroundService;
            _groupService = groupService;
            _logger = logger;
            _smsMonitor = smsSettings;
            _httpClientFactory = httpClientFactory;
            _env = env;
            _configuration = configuration;
        }

        [BindProperty]
        public SmsTestModel TestMessage { get; set; } = new();

        public IEnumerable<Models.Group> AvailableGroups { get; set; } = new List<Models.Group>();
        public SmsServiceStatus ServiceStatus { get; set; } = new();
        public bool TestModeActive => Settings.TestMode;
        public string CurrentProviderType => Settings.ProviderType ?? "REST";
        public string SettingsJson { get; private set; } = "{}";

        public async Task OnGetAsync()
        {
            try
            {
                AvailableGroups = await _groupService.GetAllGroupsAsync();
                ServiceStatus = _smsBackgroundService.GetServiceStatus();
                SettingsJson = JsonSerializer.Serialize(new
                {
                    providerType = Settings.ProviderType ?? "REST",
                    apiEndpoint = Settings.ApiEndpoint ?? "",
                    httpMethod = Settings.HttpMethod ?? "POST",
                    contentType = Settings.ContentType ?? "application/x-www-form-urlencoded",
                    apiParams = Settings.ApiParams ?? "",
                    apiHeaders = Settings.ApiHeaders ?? "",
                    restAuthType = Settings.RestAuthType ?? "None",
                    restBearerToken = string.IsNullOrEmpty(Settings.RestBearerToken) ? "" : "***",
                    restApiKeyName = Settings.RestApiKeyName ?? "",
                    restApiKeyLocation = Settings.RestApiKeyLocation ?? "Header",
                    soapAction = Settings.SoapAction ?? "",
                    soapAuthType = Settings.SoapAuthType ?? "WSSecurity",
                    soapBodyTemplate = Settings.SoapBodyTemplate ?? "",
                    soapParams = Settings.SoapParams ?? "",
                    soapEnvelopeNamespaces = Settings.SoapEnvelopeNamespaces ?? "",
                    soapSendingSystem = Settings.SoapSendingSystem ?? "SCADA",
                    soapMessageType = Settings.SoapMessageType ?? "SmsType1",
                    senderName = Settings.SenderName ?? "",
                    testMode = Settings.TestMode
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading SMS test page");
                TempData["ErrorMessage"] = "Error loading page data.";
            }
        }

        public async Task<IActionResult> OnGetGroupMembersAsync(int groupId)
        {
            try
            {
                var members = await _groupService.GetGroupMembersAsync(groupId);
                var result = members.Select(u => new
                {
                    name = $"{u.FirstName} {u.LastName}".Trim().Length > 0
                        ? $"{u.FirstName} {u.LastName}".Trim()
                        : u.UserName,
                    maskedPhone = MaskPhone(u.PhoneNumber),
                    smsEnabled = u.SmsEnabled
                });
                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching group members for preview");
                return new JsonResult(Array.Empty<object>());
            }
        }

        private static string MaskPhone(string? phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length <= 4) return phone ?? "—";
            return new string('*', phone.Length - 4) + phone[^4..];
        }

        public async Task<IActionResult> OnPostSendTestAsync()
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            try
            {
                var alarmId = $"TEST-{DateTime.Now:yyyyMMdd-HHmmss}";
                var success = await _smsBackgroundService.QueueSmsMessageAsync(
                    TestMessage.Message, 
                    TestMessage.GroupId, 
                    alarmId, 
                    "normal");

                if (success)
                {
                    TempData["SuccessMessage"] = $"Test SMS queued successfully! Alarm ID: {alarmId}";
                    _logger.LogInformation("Test SMS queued for group {GroupId}", TestMessage.GroupId);
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to queue test SMS.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test SMS");
                TempData["ErrorMessage"] = "Error occurred while sending test SMS.";
            }

            return RedirectToPage();
        }

        public IActionResult OnGetRefreshStatus()
        {
            try
            {
                ServiceStatus = _smsBackgroundService.GetServiceStatus();
                return new JsonResult(new
                {
                    queueSize = ServiceStatus.QueueSize,
                    messagesSent = ServiceStatus.MessagesSent,
                    messagesFailed = ServiceStatus.MessagesFailed,
                    uptime = ServiceStatus.ServiceUptime.ToString(@"d\.hh\:mm\:ss"),
                    lastMessage = ServiceStatus.LastMessageTime?.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing service status");
                return BadRequest("Error refreshing status");
            }
        }

        public async Task<IActionResult> OnPostTestHealthAsync()
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.GetAsync("http://localhost:5000/api/sms/health");
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = $"Health check passed! Response: {content}";
                }
                else
                {
                    TempData["ErrorMessage"] = $"Health check failed! Status: {response.StatusCode}, Response: {content}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing health endpoint");
                TempData["ErrorMessage"] = "Error occurred while testing health endpoint.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostFireSoapAsync([FromBody] FireSoapRequest req)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (string.IsNullOrWhiteSpace(req.Envelope))
                    return new JsonResult(new { success = false, errorMessage = "Envelope is required" }) { StatusCode = 400 };

                var endpoint = req.TargetMock
                    ? $"{Request.Scheme}://{Request.Host}/mock/sms/soap"
                    : req.Endpoint;

                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out _))
                    return new JsonResult(new { success = false, errorMessage = "Invalid endpoint URL" }) { StatusCode = 400 };

                // Fall back to saved credentials when caller sends empty strings
                var username = !string.IsNullOrEmpty(req.Username) ? req.Username : (Settings.Username ?? string.Empty);
                var password = !string.IsNullOrEmpty(req.Password) ? req.Password : (Settings.Password ?? string.Empty);

                var envelope = req.Envelope;

                // Inject WS-Security header if auth type requires it and it's not already present
                if (req.AuthType.Equals("WSSecurity", StringComparison.OrdinalIgnoreCase) &&
                    !envelope.Contains("<Security", StringComparison.OrdinalIgnoreCase))
                {
                    var wsHeader = BuildWsSecurityHeader(username, password);
                    envelope = envelope
                        .Replace("<soapenv:Header/>", wsHeader, StringComparison.Ordinal)
                        .Replace("<soapenv:Header />", wsHeader, StringComparison.Ordinal);
                    if (!envelope.Contains("<Security", StringComparison.OrdinalIgnoreCase))
                        _logger.LogWarning("FireSoap: WSSecurity injection requested but no matching empty header tag found — Security header was not injected");
                }

                var client = _httpClientFactory.CreateClient("SoapProbe");
                using var httpReq = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpReq.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");
                httpReq.Headers.TryAddWithoutValidation("SOAPAction", $"\"{req.SoapAction}\"");

                if (req.AuthType.Equals("HttpBasic", StringComparison.OrdinalIgnoreCase))
                {
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                    httpReq.Headers.TryAddWithoutValidation("Authorization", $"Basic {credentials}");
                }

                var httpResp = await client.SendAsync(httpReq);
                sw.Stop();
                var body = await httpResp.Content.ReadAsStringAsync();

                var successPattern = Settings.SoapSuccessPattern;
                var errorPattern = Settings.SoapErrorPattern;
                var success = string.IsNullOrEmpty(successPattern)
                    ? httpResp.IsSuccessStatusCode
                    : body.Contains(successPattern, StringComparison.OrdinalIgnoreCase);

                string? errorMessage = null;
                if (!success && !string.IsNullOrEmpty(errorPattern))
                {
                    var start = body.IndexOf(errorPattern, StringComparison.OrdinalIgnoreCase);
                    if (start >= 0)
                    {
                        start += errorPattern.Length;
                        var end = body.IndexOf('<', start);
                        if (end > start) errorMessage = body[start..end].Trim();
                    }
                }

                // Redact credentials in echo envelope
                var echoEnvelope = string.IsNullOrEmpty(password)
                    ? envelope
                    : envelope.Replace(XmlEscape(password), "***", StringComparison.Ordinal)
                              .Replace(password, "***", StringComparison.Ordinal);

                _logger.LogInformation("FireSoap: {StatusCode} from {Endpoint} in {ElapsedMs}ms",
                    (int)httpResp.StatusCode, endpoint, sw.ElapsedMilliseconds);

                return new JsonResult(new
                {
                    statusCode = (int)httpResp.StatusCode,
                    elapsedMs = sw.ElapsedMilliseconds,
                    body,
                    success,
                    errorMessage,
                    echoEnvelope
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Error in FireSoap handler");
                return new JsonResult(new
                {
                    success = false,
                    errorMessage = ex.Message,
                    statusCode = 0,
                    elapsedMs = sw.ElapsedMilliseconds,
                    body = (string?)null,
                    echoEnvelope = (string?)null
                }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostSaveSoapConfigAsync([FromBody] SaveSoapConfigRequest req)
        {
            try
            {
                var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
                var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                var root = JsonNode.Parse(json)!.AsObject();

                // Clone existing SmsSettings node so non-SOAP fields are preserved
                var existing = root["SmsSettings"]?.AsObject() ?? new JsonObject();

                existing["ProviderType"]           = "SOAP";
                existing["ApiEndpoint"]            = req.ApiEndpoint;
                existing["SoapAction"]             = req.SoapAction;
                existing["SoapBodyTemplate"]       = req.SoapBodyTemplate;
                existing["SoapParams"]             = req.SoapParams;
                existing["SoapEnvelopeNamespaces"] = req.SoapEnvelopeNamespaces;
                existing["SoapAuthType"]           = req.SoapAuthType;
                existing["SoapSendingSystem"]      = req.SoapSendingSystem;
                existing["SoapMessageType"]        = req.SoapMessageType;
                existing["Username"]               = req.Username;
                if (!string.IsNullOrEmpty(req.Password))
                    existing["Password"]           = req.Password;

                root["SmsSettings"] = existing;

                var writeOptions = new JsonSerializerOptions { WriteIndented = true };
                await System.IO.File.WriteAllTextAsync(appSettingsPath, root.ToJsonString(writeOptions));

                if (_configuration is IConfigurationRoot configRoot)
                    configRoot.Reload();

                _logger.LogInformation("SOAP config saved from workbench by user");
                return new JsonResult(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving SOAP config from workbench");
                return new JsonResult(new { success = false, message = ex.Message }) { StatusCode = 500 };
            }
        }

        private static string BuildWsSecurityHeader(string username, string password) =>
            $@"  <soapenv:Header>
    <Security xmlns=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"">
      <UsernameToken>
        <Username>{XmlEscape(username)}</Username>
        <Password Type=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText"">{XmlEscape(password)}</Password>
      </UsernameToken>
    </Security>
  </soapenv:Header>";

        private static string XmlEscape(string value) =>
            value.Replace("&", "&amp;")
                 .Replace("<", "&lt;")
                 .Replace(">", "&gt;")
                 .Replace("\"", "&quot;")
                 .Replace("'", "&apos;");
    }

    public class SmsTestModel
    {
        [Required]
        [StringLength(500, MinimumLength = 5)]
        [Display(Name = "Test Message")]
        public string Message { get; set; } = "Test SMS from SCADA System";

        [Required]
        [Display(Name = "Target Group")]
        public int GroupId { get; set; }
    }

    public class FireSoapRequest
    {
        public string Endpoint { get; set; } = string.Empty;
        public string SoapAction { get; set; } = string.Empty;
        public string Envelope { get; set; } = string.Empty;
        public string AuthType { get; set; } = "None";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool TargetMock { get; set; }
    }

    public class SaveSoapConfigRequest
    {
        public string ApiEndpoint { get; set; } = string.Empty;
        public string SoapAction { get; set; } = string.Empty;
        public string SoapBodyTemplate { get; set; } = string.Empty;
        public string SoapParams { get; set; } = string.Empty;
        public string SoapEnvelopeNamespaces { get; set; } = string.Empty;
        public string SoapAuthType { get; set; } = string.Empty;
        public string SoapSendingSystem { get; set; } = string.Empty;
        public string SoapMessageType { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}