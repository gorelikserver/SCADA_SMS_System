using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Test
{
    /// <summary>
    /// Sends a test alarm to a group through exactly the path Scada_sms.bat uses (synchronous,
    /// per-recipient, recorded in the delivery table and audit), and shows each recipient's result.
    /// Provider configuration and single-number verification live in Settings > Provider.
    /// </summary>
    public class SmsModel : PageModel
    {
        private readonly ISmsService _smsService;
        private readonly IGroupService _groupService;
        private readonly IOptionsMonitor<SmsSettings> _smsSettings;
        private readonly ILogger<SmsModel> _logger;

        public SmsModel(ISmsService smsService, IGroupService groupService, IOptionsMonitor<SmsSettings> smsSettings, ILogger<SmsModel> logger)
        {
            _smsService = smsService;
            _groupService = groupService;
            _smsSettings = smsSettings;
            _logger = logger;
        }

        [BindProperty]
        public SmsTestModel TestMessage { get; set; } = new();

        public IEnumerable<Group> AvailableGroups { get; private set; } = new List<Group>();
        public bool TestModeActive => _smsSettings.CurrentValue.TestMode;
        public string? ProviderName => _smsSettings.CurrentValue.Profile?.Name;
        public CriticalSmsDeliveryResult? Result { get; private set; }

        public async Task OnGetAsync()
        {
            AvailableGroups = await _groupService.GetAllGroupsAsync();
        }

        public async Task<IActionResult> OnGetGroupMembersAsync(int groupId)
        {
            try
            {
                var members = await _groupService.GetGroupMembersAsync(groupId);
                return new JsonResult(members.Select(u => new
                {
                    name = $"{u.FirstName} {u.LastName}".Trim().Length > 0 ? $"{u.FirstName} {u.LastName}".Trim() : u.UserName,
                    maskedPhone = MaskPhone(u.PhoneNumber),
                    smsEnabled = u.SmsEnabled
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching group members for preview");
                return new JsonResult(Array.Empty<object>());
            }
        }

        public async Task<IActionResult> OnPostSendAsync()
        {
            AvailableGroups = await _groupService.GetAllGroupsAsync();
            if (!ModelState.IsValid)
                return Page();

            var alarmId = $"TEST-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
            _logger.LogWarning("{User} sent test alarm {AlarmId} to group {GroupId}", User.Identity?.Name, alarmId, TestMessage.GroupId);
            Result = await _smsService.SendCriticalSmsToGroupAsync(
                TestMessage.Message, TestMessage.GroupId, alarmId, "normal", null, HttpContext.RequestAborted);
            return Page();
        }

        private static string MaskPhone(string? phone) =>
            string.IsNullOrEmpty(phone) || phone.Length <= 4 ? phone ?? "—" : new string('*', phone.Length - 4) + phone[^4..];
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
