using Microsoft.AspNetCore.Mvc;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Shared.Components.SystemAlerts
{
    /// <summary>Site-wide banner: test mode, undelivered critical alarms, unreadable spool files, empty groups.</summary>
    public class SystemAlertsViewComponent : ViewComponent
    {
        private readonly IDeliveryAlertService _alerts;
        private readonly ILogger<SystemAlertsViewComponent> _logger;

        public SystemAlertsViewComponent(IDeliveryAlertService alerts, ILogger<SystemAlertsViewComponent> logger)
        {
            _alerts = alerts;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                return View(await _alerts.GetSummaryAsync(HttpContext.RequestAborted));
            }
            catch (Exception ex)
            {
                // The banner must never break a page; say that the check itself failed instead.
                _logger.LogError(ex, "Could not load delivery alerts for banner");
                return View((DeliveryAlertSummary?)null);
            }
        }
    }
}
