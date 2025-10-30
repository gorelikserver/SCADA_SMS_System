using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SCADASMSSystem.Web.Pages.Test
{
    public class SmsModel : PageModel
    {
        private readonly SmsBackgroundService _smsBackgroundService;
        private readonly IGroupService _groupService;
        private readonly ILogger<SmsModel> _logger;

        public SmsModel(SmsBackgroundService smsBackgroundService, IGroupService groupService, ILogger<SmsModel> logger)
        {
            _smsBackgroundService = smsBackgroundService;
            _groupService = groupService;
            _logger = logger;
        }

        [BindProperty]
        public SmsTestModel TestMessage { get; set; } = new();

        public IEnumerable<Models.Group> AvailableGroups { get; set; } = new List<Models.Group>();
        public SmsServiceStatus ServiceStatus { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                AvailableGroups = await _groupService.GetAllGroupsAsync();
                ServiceStatus = _smsBackgroundService.GetServiceStatus();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading SMS test page");
                TempData["ErrorMessage"] = "Error loading page data.";
            }
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