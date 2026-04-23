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
                    username = Settings.Username ?? "",
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
                using var httpClient = new HttpClient();
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
}