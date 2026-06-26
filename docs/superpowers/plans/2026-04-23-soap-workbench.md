# SOAP Workbench Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Postman-style interactive SOAP workbench tab to `/Test/Sms` — loads from saved settings, lets you edit envelope/params ad-hoc, fire against real or mock endpoint, see live response, and save config back to settings.

**Architecture:** Three-part change — (1) register a named `"SoapProbe"` HttpClient and inject new deps into `SmsModel`; (2) add two server-side handlers (`FireSoap`, `SaveSoapConfig`) to the Test page model; (3) add a tab bar + Alpine.js `soapWorkbench()` component to the Razor view. No new files, no new routes. All changes are additive.

**Tech Stack:** ASP.NET Core Razor Pages, Alpine.js 3, Bootstrap 5, `System.Text.Json.Nodes`, `IHttpClientFactory`

---

## File Map

| File | Change |
|---|---|
| `Program.cs` | Register named HttpClient `"SoapProbe"` + inject `IWebHostEnvironment` to `SmsModel` |
| `Pages/Test/Sms.cshtml.cs` | Add `IHttpClientFactory`, `IWebHostEnvironment`, `IConfiguration` to ctor; add `FireSoapRequest`, `SaveSoapConfigRequest` DTOs; add `OnPostFireSoapAsync`, `OnPostSaveSoapConfigAsync` handlers; update `SettingsJson` to include `username` |
| `Pages/Test/Sms.cshtml` | Add top-level tab bar, wrap existing content in "SMS Test" tab pane, add "SOAP Workbench" tab pane with full HTML + `soapWorkbench()` Alpine component |

---

## Task 1: Register SoapProbe HttpClient and update SmsModel dependencies

**Files:**
- Modify: `Program.cs:94`
- Modify: `Pages/Test/Sms.cshtml.cs:14-27`

- [ ] **Step 1: Register the named HttpClient in Program.cs**

After line `builder.Services.AddHttpClient<SmsService>();` (line 94), add:

```csharp
// Named HttpClient for SOAP workbench probe — separate from SmsService client
builder.Services.AddHttpClient("SoapProbe", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

- [ ] **Step 2: Update SmsModel constructor to accept new dependencies**

Replace the `SmsModel` class constructor area (lines 14–27) in `Pages/Test/Sms.cshtml.cs`:

```csharp
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
```

- [ ] **Step 3: Add `username` to SettingsJson in `OnGetAsync`**

In `OnGetAsync`, add `username = Settings.Username ?? ""` to the anonymous object passed to `JsonSerializer.Serialize`. The object currently ends at `testMode = Settings.TestMode`. Add before the closing `}`:

```csharp
                    soapSendingSystem = Settings.SoapSendingSystem ?? "SCADA",
                    soapMessageType = Settings.SoapMessageType ?? "SmsType1",
                    senderName = Settings.SenderName ?? "",
                    username = Settings.Username ?? "",   // ← add this line
                    testMode = Settings.TestMode
```

- [ ] **Step 4: Build and verify no compile errors**

```bash
dotnet build c:/SCADA_CSharp_Clean/SCADASMSSystem.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git -C c:/SCADA_CSharp_Clean add Program.cs Pages/Test/Sms.cshtml.cs
git -C c:/SCADA_CSharp_Clean commit -m "feat: register SoapProbe HttpClient, inject deps into SmsModel"
```

---

## Task 2: Add DTOs and FireSoap handler

**Files:**
- Modify: `Pages/Test/Sms.cshtml.cs` — add DTOs and handler after `OnGetRefreshStatus`, before closing brace of `SmsModel`

- [ ] **Step 1: Add `FireSoapRequest` DTO and `OnPostFireSoapAsync` handler**

Add the following code **before** the closing `}` of the `SmsModel` class (before `public class SmsTestModel`):

```csharp
        [IgnoreAntiforgeryToken]
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
                var username = !string.IsNullOrEmpty(req.Username) ? req.Username : Settings.Username;
                var password = !string.IsNullOrEmpty(req.Password) ? req.Password : Settings.Password;

                var envelope = req.Envelope;

                // Inject WS-Security header if auth type requires it and it's not already present
                if (req.AuthType.Equals("WSSecurity", StringComparison.OrdinalIgnoreCase) &&
                    !envelope.Contains("<Security", StringComparison.OrdinalIgnoreCase))
                {
                    var wsHeader = BuildWsSecurityHeader(username, password);
                    envelope = envelope
                        .Replace("<soapenv:Header/>", wsHeader, StringComparison.Ordinal)
                        .Replace("<soapenv:Header />", wsHeader, StringComparison.Ordinal);
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
```

- [ ] **Step 2: Add `FireSoapRequest` DTO at the bottom of the file**

After the `SmsTestModel` class, add:

```csharp
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
```

- [ ] **Step 3: Build**

```bash
dotnet build c:/SCADA_CSharp_Clean/SCADASMSSystem.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git -C c:/SCADA_CSharp_Clean add Pages/Test/Sms.cshtml.cs
git -C c:/SCADA_CSharp_Clean commit -m "feat: add FireSoap handler and DTO to SmsModel"
```

---

## Task 3: Add SaveSoapConfig handler

**Files:**
- Modify: `Pages/Test/Sms.cshtml.cs` — add handler and DTO

- [ ] **Step 1: Add `OnPostSaveSoapConfigAsync` handler**

Add after `OnPostFireSoapAsync` (before `BuildWsSecurityHeader`):

```csharp
        [IgnoreAntiforgeryToken]
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
```

- [ ] **Step 2: Add `SaveSoapConfigRequest` DTO at the bottom of the file**

After `FireSoapRequest`:

```csharp
    public class SaveSoapConfigRequest
    {
        public string ApiEndpoint { get; set; } = string.Empty;
        public string SoapAction { get; set; } = string.Empty;
        public string SoapBodyTemplate { get; set; } = string.Empty;
        public string SoapParams { get; set; } = string.Empty;
        public string SoapEnvelopeNamespaces { get; set; } = string.Empty;
        public string SoapAuthType { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
```

- [ ] **Step 3: Build**

```bash
dotnet build c:/SCADA_CSharp_Clean/SCADASMSSystem.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git -C c:/SCADA_CSharp_Clean add Pages/Test/Sms.cshtml.cs
git -C c:/SCADA_CSharp_Clean commit -m "feat: add SaveSoapConfig handler and DTO to SmsModel"
```

---

## Task 4: Add tab bar and wrap existing content in SMS Test tab

**Files:**
- Modify: `Pages/Test/Sms.cshtml`

- [ ] **Step 1: Add tab bar after the page heading block**

The page currently opens with a `<div class="container-fluid">` followed by the heading block. After the heading `d-sm-flex` div (which ends around line 30), before the Toast triggers section, insert the tab bar:

```html
    <!-- ═══ Top-level tab bar ═══ -->
    <ul class="nav nav-tabs mb-3" id="testPageTabs" role="tablist">
        <li class="nav-item" role="presentation">
            <button class="nav-link @(Model.CurrentProviderType == "SOAP" ? "" : "active")"
                    id="tab-sms-test-btn"
                    data-bs-toggle="tab" data-bs-target="#tab-sms-test"
                    type="button" role="tab">
                <i class="fas fa-paper-plane me-1"></i>SMS Test
            </button>
        </li>
        <li class="nav-item" role="presentation">
            <button class="nav-link @(Model.CurrentProviderType == "SOAP" ? "active" : "")"
                    id="tab-soap-workbench-btn"
                    data-bs-toggle="tab" data-bs-target="#tab-soap-workbench"
                    type="button" role="tab">
                <i class="fas fa-soap me-1"></i>SOAP Workbench
                <span class="badge bg-warning text-dark ms-1" style="font-size:.62rem;">SOAP</span>
            </button>
        </li>
    </ul>

    <div class="tab-content" id="testPageTabsContent">
```

- [ ] **Step 2: Wrap the existing main content in the SMS Test tab pane**

The existing main content starts at `<!-- Toast triggers -->` and ends at the closing `</div>` of the container-fluid. Wrap everything from the Toast triggers through (and including) the `@if (Model.TestModeActive)` mock tester block in:

```html
    <!-- ═══ SMS TEST TAB ═══ -->
    <div class="tab-pane fade @(Model.CurrentProviderType == "SOAP" ? "" : "show active")"
         id="tab-sms-test" role="tabpanel">

      {{existing content here}}

    </div><!-- /tab-sms-test -->
```

- [ ] **Step 3: Add empty SOAP Workbench tab pane placeholder**

After the `</div><!-- /tab-sms-test -->`, add:

```html
    <!-- ═══ SOAP WORKBENCH TAB ═══ -->
    <div class="tab-pane fade @(Model.CurrentProviderType == "SOAP" ? "show active" : "")"
         id="tab-soap-workbench" role="tabpanel">
        <!-- content added in Task 5 -->
    </div><!-- /tab-soap-workbench -->

    </div><!-- /tab-content -->
```

- [ ] **Step 4: Build and verify page loads with tab bar visible**

```bash
dotnet build c:/SCADA_CSharp_Clean/SCADASMSSystem.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

Start the app and navigate to `/Test/Sms` — confirm the tab bar renders, SMS Test tab shows existing UI, SOAP Workbench tab is empty.

- [ ] **Step 5: Commit**

```bash
git -C c:/SCADA_CSharp_Clean add Pages/Test/Sms.cshtml
git -C c:/SCADA_CSharp_Clean commit -m "feat: add tab bar to Test/Sms page, wrap existing UI in SMS Test tab"
```

---

## Task 5: Add SOAP Workbench HTML — left column (request builder)

**Files:**
- Modify: `Pages/Test/Sms.cshtml` — replace `<!-- content added in Task 5 -->` placeholder

- [ ] **Step 1: Replace the placeholder with the full workbench layout**

Replace `<!-- content added in Task 5 -->` with:

```html
    @if (Model.CurrentProviderType != "SOAP")
    {
        <div class="alert alert-info mt-3">
            <i class="fas fa-info-circle me-2"></i>
            Switch provider to <strong>SOAP</strong> in <a href="/Settings">Settings</a> to use the workbench.
        </div>
    }
    else
    {
    <div x-data="soapWorkbench()" class="mt-2">
        <div class="row g-4">

            <!-- ═══ LEFT: Request Builder ═══ -->
            <div class="col-lg-5">

                <!-- Target toggle -->
                <div class="card shadow mb-3">
                    <div class="card-body py-2 px-3">
                        <div class="d-flex align-items-center gap-3 flex-wrap">
                            <span class="fw-semibold" style="font-size:.82rem;">Target:</span>
                            <div class="btn-group btn-group-sm" role="group">
                                <button type="button" class="btn"
                                        :class="!targetMock ? 'btn-primary' : 'btn-outline-primary'"
                                        @@click="targetMock = false">
                                    <i class="fas fa-server me-1"></i>Real Endpoint
                                </button>
                                <button type="button" class="btn"
                                        :class="targetMock ? 'btn-warning text-dark' : 'btn-outline-warning'"
                                        @@click="targetMock = true">
                                    <i class="fas fa-flask me-1"></i>Mock Server
                                </button>
                            </div>
                            <span class="text-muted" style="font-size:.75rem;" x-show="targetMock">
                                → <code>/mock/sms/soap</code>
                            </span>
                        </div>
                    </div>
                </div>

                <!-- Endpoint + SOAPAction -->
                <div class="card shadow mb-3">
                    <div class="card-header card-header-gradient py-2">
                        <h6 class="m-0 fw-bold" style="font-size:.85rem;">
                            <i class="fas fa-link me-1"></i>Request
                        </h6>
                    </div>
                    <div class="card-body p-3">
                        <div class="mb-2">
                            <label class="form-label fw-semibold" style="font-size:.78rem;text-transform:uppercase;letter-spacing:.4px;">
                                Endpoint URL
                            </label>
                            <input type="url" x-model="endpoint" class="form-control form-control-sm font-monospace"
                                   placeholder="https://provider/soap" :disabled="targetMock" />
                            <div x-show="targetMock" class="text-muted mt-1" style="font-size:.74rem;">
                                Overridden by mock target
                            </div>
                        </div>
                        <div>
                            <label class="form-label fw-semibold" style="font-size:.78rem;text-transform:uppercase;letter-spacing:.4px;">
                                SOAPAction
                            </label>
                            <input type="text" x-model="soapAction" class="form-control form-control-sm font-monospace"
                                   placeholder="http://tempuri.org/..." />
                        </div>
                    </div>
                </div>

                <!-- Auth accordion -->
                <div class="card shadow mb-3">
                    <div class="card-header card-header-gradient py-2" style="cursor:pointer;"
                         @@click="showAuth = !showAuth">
                        <div class="d-flex align-items-center justify-content-between">
                            <h6 class="m-0 fw-bold" style="font-size:.85rem;">
                                <i class="fas fa-key me-1"></i>Authentication
                                <span class="badge bg-secondary ms-2 fw-normal" style="font-size:.65rem;" x-text="authType"></span>
                            </h6>
                            <i class="fas" :class="showAuth ? 'fa-chevron-up' : 'fa-chevron-down'" style="font-size:.75rem;"></i>
                        </div>
                    </div>
                    <div class="card-body p-3" x-show="showAuth" x-transition>
                        <div class="mb-2">
                            <label class="form-label" style="font-size:.78rem;">Auth Type</label>
                            <select x-model="authType" class="form-select form-select-sm">
                                <option value="WSSecurity">WS-Security (UsernameToken)</option>
                                <option value="HttpBasic">HTTP Basic</option>
                                <option value="None">None</option>
                            </select>
                        </div>
                        <div x-show="authType !== 'None'" x-transition>
                            <div class="mb-2">
                                <label class="form-label" style="font-size:.78rem;">Username</label>
                                <input type="text" x-model="username" class="form-control form-control-sm"
                                       placeholder="Username" autocomplete="off" />
                            </div>
                            <div>
                                <label class="form-label" style="font-size:.78rem;">Password</label>
                                <input type="password" x-model="password" class="form-control form-control-sm"
                                       placeholder="Leave blank to use saved password" autocomplete="new-password" />
                                <div class="text-muted mt-1" style="font-size:.72rem;">
                                    <i class="fas fa-info-circle me-1"></i>Blank = server uses saved settings password
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Envelope editor -->
                <div class="card shadow mb-3">
                    <div class="card-header card-header-gradient py-2">
                        <div class="d-flex align-items-center justify-content-between">
                            <h6 class="m-0 fw-bold" style="font-size:.85rem;">
                                <i class="fas fa-code me-1"></i>Envelope
                            </h6>
                            <div class="btn-group btn-group-sm">
                                <button type="button" class="btn btn-sm"
                                        :class="editorTab==='template' ? 'btn-primary' : 'btn-outline-primary'"
                                        @@click="switchToTemplate()">Template</button>
                                <button type="button" class="btn btn-sm"
                                        :class="editorTab==='raw' ? 'btn-secondary' : 'btn-outline-secondary'"
                                        @@click="switchToRaw()">Raw XML</button>
                            </div>
                        </div>
                    </div>
                    <div class="card-body p-3">

                        <!-- Template mode -->
                        <div x-show="editorTab==='template'" x-transition>
                            <div x-show="!soapBodyTemplate" class="alert alert-warning py-2 mb-2" style="font-size:.78rem;">
                                <i class="fas fa-exclamation-triangle me-1"></i>
                                No SOAP Body Template configured. <a href="/Settings">Configure in Settings</a>.
                            </div>
                            <template x-if="soapBodyTemplate">
                                <div>
                                    <!-- Dynamic input fields for detected tokens -->
                                    <div class="mb-3">
                                        <label class="form-label fw-semibold" style="font-size:.78rem;text-transform:uppercase;letter-spacing:.4px;">
                                            Placeholder Values
                                        </label>
                                        <template x-for="token in detectedInputTokens" :key="token">
                                            <div class="mb-2">
                                                <label class="form-label" style="font-size:.74rem;"
                                                       x-text="tokenLabel(token)"></label>
                                                <input type="text"
                                                       :value="inputs[tokenKey(token)] ?? ''"
                                                       @@input="inputs[tokenKey(token)] = $event.target.value"
                                                       class="form-control form-control-sm"
                                                       :placeholder="tokenPlaceholder(token)" />
                                            </div>
                                        </template>
                                        <div x-show="detectedInputTokens.length === 0" class="text-muted" style="font-size:.78rem;">
                                            No dynamic placeholders detected in template.
                                        </div>
                                    </div>
                                    <!-- Live XML preview -->
                                    <label class="form-label fw-semibold" style="font-size:.78rem;text-transform:uppercase;letter-spacing:.4px;">
                                        Assembled Envelope Preview
                                    </label>
                                    <pre class="m-0 p-2 rounded" style="font-size:.68rem;background:#1e1e1e;color:#d4d4d4;max-height:220px;overflow-y:auto;white-space:pre-wrap;word-break:break-all;"
                                         x-text="assembledEnvelope"></pre>
                                </div>
                            </template>
                        </div>

                        <!-- Raw XML mode -->
                        <div x-show="editorTab==='raw'" x-transition>
                            <div class="d-flex align-items-center justify-content-between mb-1">
                                <span class="text-muted" style="font-size:.74rem;">Edit full SOAP envelope XML</span>
                                <button type="button" class="btn btn-sm btn-outline-secondary" style="font-size:.7rem;padding:.1rem .4rem;"
                                        @@click="switchToTemplate()">
                                    <i class="fas fa-arrow-left me-1"></i>Back to Template
                                </button>
                            </div>
                            <textarea x-model="rawEnvelope" class="form-control font-monospace"
                                      rows="12" style="font-size:.7rem;resize:vertical;"></textarea>
                        </div>
                    </div>
                </div>

                <!-- Action buttons -->
                <div class="d-flex gap-2 flex-wrap">
                    <button type="button" class="btn btn-warning flex-grow-1"
                            @@click="send()" :disabled="sending">
                        <span x-show="sending" class="spinner-border spinner-border-sm me-1"></span>
                        <i class="fas fa-paper-plane me-1" x-show="!sending"></i>
                        <span x-text="sending ? 'Sending…' : 'Send'"></span>
                    </button>
                    <button type="button" class="btn btn-outline-secondary"
                            @@click="loadFromSettings()" title="Reload from saved settings">
                        <i class="fas fa-sync-alt"></i>
                    </button>
                    <button type="button" class="btn btn-outline-success"
                            @@click="saveToSettings()" :disabled="saving" title="Save workbench config to settings">
                        <span x-show="saving" class="spinner-border spinner-border-sm"></span>
                        <i class="fas fa-save" x-show="!saving"></i>
                        <span class="ms-1 d-none d-sm-inline" x-text="saving ? 'Saving…' : 'Save to Settings'"></span>
                    </button>
                </div>

            </div><!-- /left col -->
```

- [ ] **Step 2: Build**

```bash
dotnet build c:/SCADA_CSharp_Clean/SCADASMSSystem.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git -C c:/SCADA_CSharp_Clean add Pages/Test/Sms.cshtml
git -C c:/SCADA_CSharp_Clean commit -m "feat: add SOAP Workbench left column (request builder) HTML"
```

---

## Task 6: Add SOAP Workbench HTML — right column (response panel)

**Files:**
- Modify: `Pages/Test/Sms.cshtml` — add right column after the left column closing `</div>`

- [ ] **Step 1: Add the right column and close the Alpine x-data wrapper**

After `</div><!-- /left col -->`, add:

```html
            <!-- ═══ RIGHT: Response Panel ═══ -->
            <div class="col-lg-7">
                <div class="card shadow h-100">
                    <div class="card-header card-header-gradient py-2">
                        <div class="d-flex align-items-center justify-content-between flex-wrap gap-2">
                            <h6 class="m-0 fw-bold" style="font-size:.85rem;">
                                <i class="fas fa-inbox me-1"></i>Response
                            </h6>
                            <div class="d-flex align-items-center gap-2 flex-wrap">
                                <template x-if="lastStatusCode !== null">
                                    <span class="badge"
                                          :class="lastStatusCode >= 200 && lastStatusCode < 300 ? 'bg-success' : 'bg-danger'"
                                          x-text="lastStatusCode + (lastStatusCode === 200 ? ' OK' : ' Error')"></span>
                                </template>
                                <template x-if="lastElapsedMs !== null">
                                    <span class="badge bg-secondary" style="font-size:.65rem;"
                                          x-text="lastElapsedMs + ' ms'"></span>
                                </template>
                                <template x-if="lastSuccess !== null">
                                    <span class="badge"
                                          :class="lastSuccess ? 'bg-success' : 'bg-danger'"
                                          x-text="lastSuccess ? '✓ SUCCESS' : '✗ FAILURE'"></span>
                                </template>
                            </div>
                        </div>
                    </div>
                    <div class="card-body p-3 d-flex flex-column gap-3">

                        <!-- Empty state -->
                        <template x-if="lastStatusCode === null">
                            <div class="d-flex flex-column align-items-center justify-content-center text-muted py-5" style="min-height:200px;">
                                <i class="fas fa-paper-plane fa-3x mb-3 text-secondary opacity-50"></i>
                                <div style="font-size:.85rem;">Fire a request to see the response here</div>
                            </div>
                        </template>

                        <!-- Error message -->
                        <template x-if="lastErrorMessage">
                            <div class="alert alert-danger py-2 mb-0" style="font-size:.78rem;">
                                <i class="fas fa-exclamation-triangle me-1"></i>
                                <strong>Provider Error:</strong> <span x-text="lastErrorMessage"></span>
                            </div>
                        </template>

                        <!-- Response body -->
                        <template x-if="lastStatusCode !== null">
                            <div>
                                <div class="fw-semibold mb-1" style="font-size:.78rem;color:#6c757d;text-transform:uppercase;letter-spacing:.3px;">
                                    Response Body
                                </div>
                                <pre class="m-0 p-3 rounded" style="font-size:.72rem;background:#1e1e1e;color:#d4d4d4;min-height:120px;max-height:300px;overflow-y:auto;white-space:pre-wrap;word-break:break-all;"
                                     x-text="lastResponseBody || '(empty response)'"></pre>
                            </div>
                        </template>

                        <!-- Request echo accordion -->
                        <template x-if="lastEchoEnvelope">
                            <div>
                                <button type="button" class="btn btn-sm btn-outline-secondary w-100 text-start"
                                        @@click="showEcho = !showEcho" style="font-size:.78rem;">
                                    <i class="fas me-1" :class="showEcho ? 'fa-chevron-up' : 'fa-chevron-down'"></i>
                                    Request Sent (credentials redacted)
                                </button>
                                <pre x-show="showEcho" x-transition
                                     class="m-0 mt-1 p-3 rounded" style="font-size:.68rem;background:#0d1117;color:#c9d1d9;max-height:200px;overflow-y:auto;white-space:pre-wrap;word-break:break-all;"
                                     x-text="lastEchoEnvelope"></pre>
                            </div>
                        </template>

                    </div>
                </div>
            </div><!-- /right col -->

        </div><!-- /row -->
    </div><!-- /x-data soapWorkbench -->
    }
```

- [ ] **Step 2: Build**

```bash
dotnet build c:/SCADA_CSharp_Clean/SCADASMSSystem.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git -C c:/SCADA_CSharp_Clean add Pages/Test/Sms.cshtml
git -C c:/SCADA_CSharp_Clean commit -m "feat: add SOAP Workbench right column (response panel) HTML"
```

---

## Task 7: Add soapWorkbench() Alpine.js component

**Files:**
- Modify: `Pages/Test/Sms.cshtml` — add inside the `@section Scripts` block, before `</script>` of the existing script

- [ ] **Step 1: Add the `soapWorkbench()` function**

In the `@section Scripts` block, add the following function after the `smsPreviewer()` function and before `</script>`:

```javascript
        function soapWorkbench() {
            return {
                // ── Request state ──────────────────────────────
                endpoint: '',
                soapAction: '',
                authType: 'WSSecurity',
                username: '',
                password: '',
                soapBodyTemplate: '',
                soapParams: {},           // parsed from settings.soapParams JSON
                soapEnvelopeNamespaces: '',
                inputs: {
                    message: 'Test alarm from SCADA',
                    phone: '+972501234567',
                    tz: '',
                    firstName: '',
                    lastName: '',
                    senderName: '',
                    sendingSystem: 'SCADA',
                    messageType: 'SmsType1',
                },
                targetMock: false,
                editorTab: 'template',    // 'template' | 'raw'
                rawEnvelope: '',
                showAuth: false,

                // ── Response state ─────────────────────────────
                sending: false,
                lastStatusCode: null,
                lastElapsedMs: null,
                lastSuccess: null,
                lastErrorMessage: null,
                lastResponseBody: '',
                lastEchoEnvelope: '',
                showEcho: false,
                saving: false,

                // ── Init ───────────────────────────────────────
                init() {
                    const s = JSON.parse(document.getElementById('sms-preview-settings').textContent);
                    this.endpoint = s.apiEndpoint || '';
                    this.soapAction = s.soapAction || '';
                    this.authType = s.soapAuthType || 'WSSecurity';
                    this.username = s.username || '';
                    this.soapBodyTemplate = s.soapBodyTemplate || '';
                    this.soapEnvelopeNamespaces = s.soapEnvelopeNamespaces || '';
                    this.inputs.sendingSystem = s.soapSendingSystem || 'SCADA';
                    this.inputs.messageType = s.soapMessageType || 'SmsType1';
                    this.inputs.senderName = s.senderName || '';
                    try { this.soapParams = s.soapParams ? JSON.parse(s.soapParams) : {}; } catch { this.soapParams = {}; }
                    this.rawEnvelope = this.assembledEnvelope;
                },

                // ── Computed: assembled full envelope ──────────
                get assembledEnvelope() {
                    if (!this.soapBodyTemplate) return '<!-- No SOAP Body Template configured -->';
                    let body = this.soapBodyTemplate;
                    if (Object.keys(this.soapParams).length > 0) {
                        for (const [token, keyword] of Object.entries(this.soapParams))
                            body = body.replaceAll(`{${token}}`, this.resolveKeyword(keyword));
                    } else {
                        body = body
                            .replaceAll('{Message}',       this.escXml(this.inputs.message))
                            .replaceAll('{Phone}',         this.escXml(this.inputs.phone))
                            .replaceAll('{TZ}',            this.escXml(this.inputs.tz))
                            .replaceAll('{FirstName}',     this.escXml(this.inputs.firstName))
                            .replaceAll('{LastName}',      this.escXml(this.inputs.lastName))
                            .replaceAll('{Username}',      '***')
                            .replaceAll('{Password}',      '***')
                            .replaceAll('{SenderName}',    this.escXml(this.inputs.senderName))
                            .replaceAll('{SendingSystem}', this.escXml(this.inputs.sendingSystem))
                            .replaceAll('{MessageType}',   this.escXml(this.inputs.messageType));
                    }
                    const ns = this.soapEnvelopeNamespaces ? ' ' + this.soapEnvelopeNamespaces.trim() : '';
                    const header = this.authType === 'WSSecurity'
                        ? `  <soapenv:Header>
    <Security xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
      <UsernameToken>
        <Username>***</Username>
        <Password Type="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText">***</Password>
      </UsernameToken>
    </Security>
  </soapenv:Header>`
                        : '  <soapenv:Header/>';
                    return `<?xml version="1.0" encoding="utf-8"?>
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"${ns}>
${header}
  <soapenv:Body>
${body}
  </soapenv:Body>
</soapenv:Envelope>`;
                },

                // ── Computed: which template tokens need inputs ─
                get detectedInputTokens() {
                    if (Object.keys(this.soapParams).length > 0) {
                        // SoapParams mode: tokens are the keys
                        return Object.keys(this.soapParams);
                    }
                    // Legacy mode: scan template for {Token} patterns, exclude credential tokens
                    const credTokens = new Set(['Username', 'Password']);
                    const matches = [...(this.soapBodyTemplate || '').matchAll(/\{(\w+)\}/g)];
                    return [...new Set(matches.map(m => m[1]).filter(t => !credTokens.has(t)))];
                },

                // ── Helpers ────────────────────────────────────
                tokenKey(token) {
                    // Map template token name to inputs object key
                    const map = {
                        Message: 'message', Phone: 'phone', TZ: 'tz',
                        FirstName: 'firstName', LastName: 'lastName',
                        SenderName: 'senderName', SendingSystem: 'sendingSystem',
                        MessageType: 'messageType',
                    };
                    return map[token] || token.toLowerCase();
                },

                tokenLabel(token) {
                    const labels = {
                        Message: 'Message', Phone: 'Phone Number', TZ: 'TZ (User Timezone)',
                        FirstName: 'First Name', LastName: 'Last Name',
                        SenderName: 'Sender Name', SendingSystem: 'Sending System',
                        MessageType: 'Message Type',
                    };
                    // For SoapParams mode, token is the placeholder key; value is the keyword
                    if (Object.keys(this.soapParams).length > 0) {
                        const kw = this.soapParams[token] || token;
                        return `${token} (${kw})`;
                    }
                    return labels[token] || token;
                },

                tokenPlaceholder(token) {
                    const ph = {
                        message: 'Test alarm message', phone: '+972501234567', tz: '0',
                        firstName: 'John', lastName: 'Doe', senderName: 'SCADA',
                        sendingSystem: 'SCADA', messageType: 'SmsType1',
                    };
                    return ph[this.tokenKey(token)] || '';
                },

                resolveKeyword(keyword) {
                    switch ((keyword || '').toLowerCase()) {
                        case 'message': return this.escXml(this.inputs.message);
                        case 'phone': case 'phonenumber': case 'mobile': return this.escXml(this.inputs.phone);
                        case 'tz': return this.escXml(this.inputs.tz);
                        case 'firstname': return this.escXml(this.inputs.firstName);
                        case 'lastname': return this.escXml(this.inputs.lastName);
                        case 'username': case 'user': return '***';
                        case 'password': case 'pass': return '***';
                        case 'sender_name': case 'sendername': case 'from': return this.escXml(this.inputs.senderName);
                        case 'sendingsystem': return this.escXml(this.inputs.sendingSystem);
                        case 'messagetype': return this.escXml(this.inputs.messageType);
                        default: return this.escXml(keyword); // literal static value
                    }
                },

                escXml(s) {
                    return String(s || '')
                        .replace(/&/g, '&amp;').replace(/</g, '&lt;')
                        .replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&apos;');
                },

                switchToRaw() {
                    this.rawEnvelope = this.assembledEnvelope;
                    this.editorTab = 'raw';
                },

                switchToTemplate() {
                    if (this.editorTab === 'template') return;
                    if (!confirm('Switch to Template mode? Any direct edits in Raw mode will be lost.')) return;
                    this.editorTab = 'template';
                },

                // ── Send ───────────────────────────────────────
                async send() {
                    this.sending = true;
                    this.lastStatusCode = null;
                    this.lastElapsedMs = null;
                    this.lastSuccess = null;
                    this.lastErrorMessage = null;
                    this.lastResponseBody = '';
                    this.lastEchoEnvelope = '';
                    this.showEcho = false;
                    try {
                        const envelope = this.editorTab === 'raw' ? this.rawEnvelope : this.assembledEnvelope;
                        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                        const res = await fetch('?handler=FireSoap', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                            body: JSON.stringify({
                                endpoint: this.endpoint,
                                soapAction: this.soapAction,
                                envelope,
                                authType: this.authType,
                                username: this.username,
                                password: this.password,
                                targetMock: this.targetMock,
                            }),
                        });
                        const data = await res.json();
                        this.lastStatusCode = data.statusCode ?? res.status;
                        this.lastElapsedMs = data.elapsedMs ?? null;
                        this.lastSuccess = data.success ?? false;
                        this.lastErrorMessage = data.errorMessage ?? null;
                        this.lastResponseBody = data.body ?? '';
                        this.lastEchoEnvelope = data.echoEnvelope ?? '';
                    } catch (e) {
                        this.lastResponseBody = 'Network error: ' + e.message;
                        this.lastSuccess = false;
                        this.lastStatusCode = 0;
                    } finally {
                        this.sending = false;
                    }
                },

                // ── Load from settings ─────────────────────────
                async loadFromSettings() {
                    try {
                        const res = await fetch('/Settings?handler=Snapshot', {
                            headers: { Accept: 'application/json' },
                            cache: 'no-store',
                        });
                        if (!res.ok) throw new Error('HTTP ' + res.status);
                        const s = await res.json();
                        this.endpoint = s.apiEndpoint || '';
                        this.soapAction = s.soapAction || '';
                        this.authType = s.soapAuthType || 'WSSecurity';
                        this.username = s.username || '';
                        this.soapBodyTemplate = s.soapBodyTemplate || '';
                        this.soapEnvelopeNamespaces = s.soapEnvelopeNamespaces || '';
                        this.inputs.sendingSystem = s.soapSendingSystem || 'SCADA';
                        this.inputs.messageType = s.soapMessageType || 'SmsType1';
                        this.inputs.senderName = s.senderName || '';
                        try { this.soapParams = s.soapParams ? JSON.parse(s.soapParams) : {}; } catch { this.soapParams = {}; }
                        this.rawEnvelope = this.assembledEnvelope;
                        if (window.SCADAUtils) SCADAUtils.showToast('Loaded from settings', 'success');
                    } catch (e) {
                        if (window.SCADAUtils) SCADAUtils.showToast('Failed to load settings: ' + e.message, 'danger');
                    }
                },

                // ── Save to settings ───────────────────────────
                async saveToSettings() {
                    this.saving = true;
                    try {
                        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                        const res = await fetch('?handler=SaveSoapConfig', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                            body: JSON.stringify({
                                apiEndpoint: this.endpoint,
                                soapAction: this.soapAction,
                                soapBodyTemplate: this.soapBodyTemplate,
                                soapParams: Object.keys(this.soapParams).length > 0
                                    ? JSON.stringify(this.soapParams) : '',
                                soapEnvelopeNamespaces: this.soapEnvelopeNamespaces,
                                soapAuthType: this.authType,
                                username: this.username,
                                password: this.password,
                            }),
                        });
                        const data = await res.json();
                        if (window.SCADAUtils)
                            SCADAUtils.showToast(
                                data.success ? 'Saved to settings' : ('Save failed: ' + (data.message || 'Unknown error')),
                                data.success ? 'success' : 'danger'
                            );
                    } catch (e) {
                        if (window.SCADAUtils) SCADAUtils.showToast('Save failed: ' + e.message, 'danger');
                    } finally {
                        this.saving = false;
                    }
                },
            };
        }
```

- [ ] **Step 2: Build**

```bash
dotnet build c:/SCADA_CSharp_Clean/SCADASMSSystem.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Manual smoke test — Template mode**

Start the app (`dotnet run`) and navigate to `/Test/Sms`.

When provider is SOAP:
1. SOAP Workbench tab should be active by default
2. Endpoint and SOAPAction fields pre-filled from settings
3. Template sub-tab shows detected placeholder input fields
4. Assembled Envelope Preview updates as you type in the inputs
5. Auth accordion expands on click, shows auth type selector

When provider is REST:
1. SMS Test tab should be active by default
2. SOAP Workbench tab should show the "Switch to SOAP in Settings" callout

- [ ] **Step 4: Manual smoke test — Fire against mock**

1. In workbench, click "Mock Server" toggle
2. Enter a phone number and message in template inputs
3. Click Send
4. Response panel should show status code, elapsed ms, success/failure indicator, raw XML response from mock

- [ ] **Step 5: Manual smoke test — Save to Settings**

1. Change endpoint URL in workbench
2. Click "Save to Settings"
3. Navigate to `/Settings` — verify endpoint field shows new value

- [ ] **Step 6: Commit**

```bash
git -C c:/SCADA_CSharp_Clean add Pages/Test/Sms.cshtml
git -C c:/SCADA_CSharp_Clean commit -m "feat: add soapWorkbench Alpine component — SOAP workbench complete"
```

---

## Task 8: Update Settings snapshot to include username

The `Settings?handler=Snapshot` response used by `loadFromSettings()` in the workbench currently omits `username`. Without it, Load from Settings won't populate the username field.

**Files:**
- Modify: `Pages/Settings/Index.cshtml.cs:78-108` (`OnGetSnapshot`)

- [ ] **Step 1: Add `username` to the snapshot anonymous object**

In `OnGetSnapshot()`, add `username = s.Username ?? "",` to the anonymous object after `senderName = s.SenderName ?? "",`:

```csharp
                    senderName = s.SenderName ?? "",
                    username = s.Username ?? "",    // ← add this line
                    testMode = s.TestMode
```

- [ ] **Step 2: Build**

```bash
dotnet build c:/SCADA_CSharp_Clean/SCADASMSSystem.Web.csproj
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Verify Load from Settings populates username**

In the workbench, click the sync button (Load from Settings). The username field in the Auth accordion should populate.

- [ ] **Step 4: Commit**

```bash
git -C c:/SCADA_CSharp_Clean add Pages/Settings/Index.cshtml.cs
git -C c:/SCADA_CSharp_Clean commit -m "feat: add username to Settings snapshot for workbench reload"
```

---

## Completion Checklist

- [ ] Tab bar renders on `/Test/Sms`, pre-selects correct tab based on provider type
- [ ] SMS Test tab: existing UI unchanged, all existing functionality works
- [ ] SOAP Workbench: endpoint, SOAPAction, auth accordion pre-filled from settings
- [ ] Template mode: detected tokens render as inputs, envelope preview updates live
- [ ] Raw mode: editable textarea, switch back to template shows confirm dialog
- [ ] Target toggle switches between real endpoint and mock
- [ ] Send → real endpoint: fires SOAP call, shows response + echo
- [ ] Send → mock server: fires against `/mock/sms/soap`, shows mock response
- [ ] Load from Settings: reloads all workbench fields from saved config
- [ ] Save to Settings: writes SOAP fields to appsettings.json, toast confirms
- [ ] REST provider: SOAP Workbench tab shows "switch to SOAP" callout
