using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Alerts
{
    public class IndexModel : PageModel
    {
        private readonly IDeliveryAlertService _alerts;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IDeliveryAlertService alerts, ILogger<IndexModel> logger)
        {
            _alerts = alerts;
            _logger = logger;
        }

        public DeliveryAlertSummary Summary { get; private set; } = new([], [], [], [], false);

        public async Task OnGetAsync()
        {
            Summary = await _alerts.GetSummaryAsync(HttpContext.RequestAborted);
        }

        public async Task<IActionResult> OnPostAcknowledgeAsync(string alarmId, string? note)
        {
            if (string.IsNullOrWhiteSpace(alarmId))
                return BadRequest();

            await _alerts.AcknowledgeAsync(alarmId, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), User.Identity?.Name);
            _logger.LogWarning("Undelivered critical alarm {AlarmId} acknowledged by {User}: {Note}", alarmId, User.Identity?.Name, note);
            TempData["SuccessMessage"] = $"Alarm {alarmId} acknowledged.";
            return RedirectToPage();
        }
    }
}
