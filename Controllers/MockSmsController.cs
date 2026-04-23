using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Controllers
{
    [ApiController]
    [Route("mock/sms")]
    public class MockSmsController : ControllerBase
    {
        private readonly IMockSmsService _mock;
        private readonly SmsSettings _settings;

        public MockSmsController(IMockSmsService mock, IOptions<SmsSettings> settings)
        {
            _mock = mock;
            _settings = settings.Value;
        }

        [HttpGet("rest")]
        [HttpPost("rest")]
        public async Task<IActionResult> MockRest()
        {
            if (!_settings.TestMode)
                return StatusCode(503, new { error = "Mock endpoint only active in TestMode" });

            // Accepts any params — simulate like a real provider
            var recipient = new SmsRecipient(
                PhoneNumber: Request.Query["phone"].FirstOrDefault()
                    ?? Request.Form["phone"].FirstOrDefault()
                    ?? "unknown",
                FirstName: null,
                LastName: null
            );
            var message = Request.Query["message"].FirstOrDefault()
                ?? Request.Form["message"].FirstOrDefault()
                ?? "(no message)";

            var result = await _mock.SimulateSmsAsync(message, recipient, "REST");
            return Ok(new
            {
                mock = true,
                success = result.Success,
                messageId = result.Response?.Contains("messageId") == true
                    ? System.Text.Json.JsonDocument.Parse(result.Response!)
                        .RootElement.GetProperty("messageId").GetString()
                    : "MOCK-UNKNOWN"
            });
        }

        [HttpPost("soap")]
        public async Task<IActionResult> MockSoap()
        {
            if (!_settings.TestMode)
                return StatusCode(503, new { error = "Mock endpoint only active in TestMode" });

            var body = await new StreamReader(Request.Body).ReadToEndAsync();

            // Extract phone from SOAP body (best-effort, works with most structures)
            var phone = ExtractXmlValue(body, "Phone") ?? ExtractXmlValue(body, "PhoneNumber") ?? "unknown";
            var message = ExtractXmlValue(body, "Message") ?? ExtractXmlValue(body, "msg") ?? "(no message)";

            var result = await _mock.SimulateSmsAsync(message, new SmsRecipient(phone), "SOAP");

            var soapResponse = result.Success
                ? $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <SendSMSResponse>
      <Success>true</Success>
      <MessageId>{System.Security.SecurityElement.Escape(result.Response ?? "")}</MessageId>
    </SendSMSResponse>
  </soap:Body>
</soap:Envelope>"
                : $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <SendSMSResponse>
      <Success>false</Success>
      <ErrorMessage>Simulated failure (3% rate)</ErrorMessage>
    </SendSMSResponse>
  </soap:Body>
</soap:Envelope>";

            return Content(soapResponse, "text/xml");
        }

        [HttpGet("status")]
        public IActionResult Status()
        {
            if (!_settings.TestMode)
                return StatusCode(503, new { error = "Mock endpoint only active in TestMode" });

            return Ok(new
            {
                testMode = true,
                totalSent = _mock.TotalSent,
                totalFailed = _mock.TotalFailed,
                recentLog = _mock.GetRecentLog()
            });
        }

        [HttpPost("reset")]
        public IActionResult Reset()
        {
            if (!_settings.TestMode)
                return StatusCode(503, new { error = "Mock endpoint only active in TestMode" });

            _mock.Reset();
            return Ok(new { reset = true });
        }

        private static string? ExtractXmlValue(string xml, string tagName)
        {
            var open = $"<{tagName}>";
            var close = $"</{tagName}>";
            var start = xml.IndexOf(open, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += open.Length;
            var end = xml.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
            return end > start ? xml[start..end] : null;
        }
    }
}
