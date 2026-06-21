using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Debug
{
    public class SoapLogModel : PageModel
    {
        private readonly SoapXmlLog _log;
        private readonly IOptionsMonitor<SmsSettings> _settings;

        public SoapLogModel(SoapXmlLog log, IOptionsMonitor<SmsSettings> settings)
        {
            _log = log;
            _settings = settings;
        }

        public IReadOnlyList<SoapXmlLog.Entry> Entries { get; private set; } = Array.Empty<SoapXmlLog.Entry>();
        public bool LoggingEnabled { get; private set; }

        public void OnGet()
        {
            Entries = _log.Entries;
            LoggingEnabled = _settings.CurrentValue.LogSoapXml;
        }

        public IActionResult OnPostClear()
        {
            _log.Clear();
            TempData["SuccessMessage"] = "SOAP XML log cleared.";
            return RedirectToPage();
        }
    }
}
