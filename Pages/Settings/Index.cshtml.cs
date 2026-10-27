using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using SCADASMSSystem.Web.Services.Providers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SCADASMSSystem.Web.Pages.Settings
{
    public class IndexModel : PageModel
    {
        private readonly IOptionsMonitor<SmsSettings> _smsSettings;
        private readonly IUserService _userService;
        private readonly IGroupService _groupService;
        private readonly IAuditService _auditService;
        private readonly IHolidayService _holidayService;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private readonly ISettingsStore _settingsStore;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IOptionsMonitor<SmsSettings> smsSettings,
            IUserService userService,
            IGroupService groupService,
            IAuditService auditService,
            IHolidayService holidayService,
            IWebHostEnvironment env,
            IConfiguration configuration,
            ISettingsStore settingsStore,
            ILogger<IndexModel> logger)
        {
            _smsSettings = smsSettings;
            _userService = userService;
            _groupService = groupService;
            _auditService = auditService;
            _holidayService = holidayService;
            _env = env;
            _configuration = configuration;
            _settingsStore = settingsStore;
            _logger = logger;
        }

        public SystemInfo SystemInformation { get; set; } = new();
        public SmsSettings CurrentSmsSettings { get; set; } = new();
        public SystemHealthStatus HealthStatus { get; set; } = new();

        [BindProperty]
        public SmsSettings SmsSettingsInput { get; set; } = new();

        [BindProperty] public string RestHeadersJson      { get; set; } = "[]";
        [BindProperty] public string RestBodyParamsJson   { get; set; } = "[]";
        [BindProperty] public string SoapPlaceholdersJson { get; set; } = "[]";

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading settings page");

                await LoadSystemInformationAsync();
                LoadSmsSettings();
                await LoadHealthStatusAsync();

                // Pre-populate form with current values
                SmsSettingsInput = CurrentSmsSettings;
                MigrateLegacySettingsToLists();

                // Ensure bind props are initialized even when lists are already populated
                if (string.IsNullOrEmpty(RestHeadersJson) || RestHeadersJson == "[]")
                    RestHeadersJson = JsonSerializer.Serialize(SmsSettingsMerge.MaskSecrets(SmsSettingsInput.RestHeaders));
                if (string.IsNullOrEmpty(RestBodyParamsJson) || RestBodyParamsJson == "[]")
                    RestBodyParamsJson = JsonSerializer.Serialize(SmsSettingsMerge.MaskSecrets(SmsSettingsInput.RestBodyParams));
                if (string.IsNullOrEmpty(SoapPlaceholdersJson) || SoapPlaceholdersJson == "[]")
                    SoapPlaceholdersJson = JsonSerializer.Serialize(SmsSettingsMerge.MaskSecrets(SmsSettingsInput.SoapPlaceholders));

                _logger.LogInformation("Settings page loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings");
                TempData["ErrorMessage"] = "Error loading system settings. Please try again.";
            }
        }

        // GET handler returning a public-safe snapshot of current SMS settings as JSON.
        // Consumed by the Test SMS page "Refresh from settings" button.
        public IActionResult OnGetSnapshot()
        {
            try
            {
                var s = _smsSettings.CurrentValue;
                var snapshot = new
                {
                    providerType = s.ProviderType ?? "REST",
                    apiEndpoint = s.ApiEndpoint ?? "",
                    httpMethod = s.HttpMethod ?? "POST",
                    contentType = s.ContentType ?? "application/x-www-form-urlencoded",
                    apiParams = s.ApiParams ?? "",
                    apiHeaders = s.ApiHeaders ?? "",
                    restAuthType = s.RestAuthType ?? "None",
                    restBearerToken = string.IsNullOrEmpty(s.RestBearerToken) ? "" : "***",
                    restApiKeyName = s.RestApiKeyName ?? "",
                    restApiKeyLocation = s.RestApiKeyLocation ?? "Header",
                    soapAction = s.SoapAction ?? "",
                    soapAuthType = s.SoapAuthType ?? "WSSecurity",
                    soapBodyTemplate = s.SoapBodyTemplate ?? "",
                    soapParams = s.SoapParams ?? "",
                    soapEnvelopeNamespaces = s.SoapEnvelopeNamespaces ?? "",
                    soapSendingSystem = s.SoapSendingSystem ?? "SCADA",
                    soapMessageType = s.SoapMessageType ?? "SmsType1",
                    senderName = s.SenderName ?? "",
                    username = string.IsNullOrEmpty(s.Username) ? "" : "***",
                    testMode = s.TestMode,
                    restHeaders      = s.RestHeaders.Select(p => new { p.Key, Value = p.IsSecret ? "***" : p.Value, p.IsSecret }),
                    restBodyParams   = s.RestBodyParams.Select(p => new { p.Key, Value = p.IsSecret ? "***" : p.Value, p.IsSecret }),
                    soapPlaceholders = s.SoapPlaceholders.Select(p => new { p.Key, Value = p.IsSecret ? "***" : p.Value, p.IsSecret }),
                    logSoapXml       = s.LogSoapXml
                };
                return new JsonResult(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building settings snapshot");
                return new JsonResult(new { error = "snapshot_failed" }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnPostSaveSettingsAsync()
        {
            // Clear auto-generated model validation. SmsSettings has non-nullable
            // string properties (defaulted to string.Empty) which makes the model
            // binder mark every empty hidden input as a required-field error,
            // silently failing the save. Our explicit checks below cover the
            // fields we actually require (JSON validity, SOAP body template).
            ModelState.Clear();

            try
            {
                // Validate JSON fields before saving
                if (!string.IsNullOrEmpty(SmsSettingsInput.ApiParams))
                {
                    try { System.Text.Json.JsonDocument.Parse(SmsSettingsInput.ApiParams); }
                    catch { ModelState.AddModelError("SmsSettingsInput.ApiParams", "ApiParams must be valid JSON"); }
                }
                if (!string.IsNullOrEmpty(SmsSettingsInput.ApiHeaders))
                {
                    try { System.Text.Json.JsonDocument.Parse(SmsSettingsInput.ApiHeaders); }
                    catch { ModelState.AddModelError("SmsSettingsInput.ApiHeaders", "ApiHeaders must be valid JSON"); }
                }

                if (!ModelState.IsValid)
                {
                    await LoadSystemInformationAsync();
                    LoadSmsSettings();
                    await LoadHealthStatusAsync();
                    TempData["ErrorMessage"] = "Validation errors: " + string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                    return Page();
                }

                // Merge only what the form actually posted. Fields the form does not render
                // (legacy credentials, ApiParams, tokens, ...) must keep their stored values.
                const string prefix = "SmsSettingsInput.";
                var postedFields = Request.Form.Keys
                    .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(k => k.Substring(prefix.Length))
                    .ToList();
                string? Posted(string key, string value) => Request.Form.ContainsKey(key) ? value : null;

                await _settingsStore.UpdateSmsSettingsAsync(section =>
                {
                    SmsSettingsMerge.ApplyPostedFields(section, SmsSettingsInput, postedFields);
                    SmsSettingsMerge.ApplyParamList(section, nameof(SmsSettings.RestHeaders),      Posted(nameof(RestHeadersJson), RestHeadersJson));
                    SmsSettingsMerge.ApplyParamList(section, nameof(SmsSettings.RestBodyParams),   Posted(nameof(RestBodyParamsJson), RestBodyParamsJson));
                    SmsSettingsMerge.ApplyParamList(section, nameof(SmsSettings.SoapPlaceholders), Posted(nameof(SoapPlaceholdersJson), SoapPlaceholdersJson));

                    // Validate the merged result, not the raw post: stored fields the form does not
                    // render still count. Throwing here aborts the write.
                    SmsSettingsMerge.RegenerateProfile(section);
                });

                _logger.LogInformation("SMS settings saved to appsettings.json by user");
                TempData["SuccessMessage"] = "Settings saved successfully. Changes are active immediately.";
            }
            catch (SettingsValidationException ex)
            {
                _logger.LogWarning("Rejected settings save: {Errors}", string.Join("; ", ex.Errors));
                await LoadSystemInformationAsync();
                LoadSmsSettings();
                await LoadHealthStatusAsync();
                TempData["ErrorMessage"] = "Settings not saved. Validation errors: " + string.Join("; ", ex.Errors);
                return Page();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Rejected settings save: posted parameter list is not valid JSON");
                TempData["ErrorMessage"] = $"Settings not saved: a parameter list was not valid JSON ({ex.Message}). Stored settings are unchanged.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings to appsettings.json");
                TempData["ErrorMessage"] = $"Failed to save settings: {ex.Message}";
            }

            return RedirectToPage();
        }

        public IActionResult OnPostEvaluateResponse([FromBody] ResponseEvaluationInput input)
        {
            try
            {
                if (input.ProviderType.Equals("SOAP", StringComparison.OrdinalIgnoreCase))
                {
                    var probe = LegacyProfileConverter.Convert(new SmsSettings
                    {
                        ProviderType = "SOAP",
                        SoapSuccessPattern = input.SuccessPattern ?? "",
                        SoapErrorPattern = input.ErrorPattern ?? ""
                    });
                    if (probe.Success.Match == MatchTypes.None)
                        return new JsonResult(new { accepted = false, message = "A SOAP success pattern is required to confirm acceptance." });
                    var verdict = ResponseEvaluator.Evaluate(probe, 200, input.ResponseBody ?? "");
                    return new JsonResult(new { accepted = verdict.Accepted, message = verdict.Accepted ? "SOAP response confirms provider acceptance." : verdict.Reason });
                }

                var body = input.ResponseBody ?? "";
                var failed = !string.IsNullOrEmpty(input.ErrorPattern) && body.Contains(input.ErrorPattern, StringComparison.OrdinalIgnoreCase);
                var accepted = !failed && (string.IsNullOrEmpty(input.SuccessPattern) || body.Contains(input.SuccessPattern, StringComparison.OrdinalIgnoreCase));
                return new JsonResult(new { accepted, message = accepted ? "REST response body satisfies configured rules." : "REST response body does not satisfy configured rules." });
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Xml.XmlException)
            {
                return new JsonResult(new { accepted = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostTestConnectionAsync()
        {
            try
            {
                // Test database connectivity
                var users = await _userService.GetAllUsersAsync();
                TempData["SuccessMessage"] = $"Database connection successful! Found {users.Count()} users.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection test failed");
                TempData["ErrorMessage"] = "Database connection test failed. Check your connection string.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCleanupDataAsync()
        {
            try
            {
                var success = await _auditService.CleanupOldAuditsAsync(90);
                if (success)
                {
                    TempData["SuccessMessage"] = "Old audit records cleaned up successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to cleanup old audit records.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data cleanup");
                TempData["ErrorMessage"] = "An error occurred during data cleanup.";
            }

            return RedirectToPage();
        }

        private void MigrateLegacySettingsToLists()
        {
            var s = _smsSettings.CurrentValue;

            if (s.RestBodyParams.Count == 0 && !string.IsNullOrWhiteSpace(s.ApiParams))
            {
                try
                {
                    var old = JsonSerializer.Deserialize<Dictionary<string, string>>(s.ApiParams) ?? new();
                    SmsSettingsInput.RestBodyParams = old
                        .Select(kvp => new SmsParam { Key = kvp.Key, Value = kvp.Value })
                        .ToList();
                    _logger.LogInformation("Auto-migrated {Count} REST body params from ApiParams", old.Count);
                }
                catch { /* bad JSON — skip */ }
            }

            if (s.RestHeaders.Count == 0 && !string.IsNullOrWhiteSpace(s.ApiHeaders))
            {
                try
                {
                    var old = JsonSerializer.Deserialize<Dictionary<string, string>>(s.ApiHeaders) ?? new();
                    SmsSettingsInput.RestHeaders = old
                        .Select(kvp => new SmsParam { Key = kvp.Key, Value = kvp.Value })
                        .ToList();
                    _logger.LogInformation("Auto-migrated {Count} REST headers from ApiHeaders", old.Count);
                }
                catch { /* bad JSON — skip */ }
            }

            if (s.SoapPlaceholders.Count == 0)
            {
                var placeholders = new List<SmsParam>();
                if (!string.IsNullOrWhiteSpace(s.SoapParams))
                {
                    try
                    {
                        var old = JsonSerializer.Deserialize<Dictionary<string, string>>(s.SoapParams) ?? new();
                        placeholders.AddRange(old.Select(kvp => new SmsParam { Key = kvp.Key, Value = kvp.Value }));
                    }
                    catch { /* bad JSON */ }
                }
                // Promote legacy credential fields into placeholder rows
                bool hasUser = placeholders.Any(p => p.Key.Equals("Username", StringComparison.OrdinalIgnoreCase));
                bool hasPass = placeholders.Any(p => p.Key.Equals("Password", StringComparison.OrdinalIgnoreCase));
                if (!hasUser && !string.IsNullOrWhiteSpace(s.Username))
                    placeholders.Add(new SmsParam { Key = "Username", Value = s.Username, IsSecret = false });
                if (!hasPass && !string.IsNullOrWhiteSpace(s.Password))
                    placeholders.Add(new SmsParam { Key = "Password", Value = s.Password, IsSecret = true });
                if (!string.IsNullOrWhiteSpace(s.SenderName))
                    placeholders.Add(new SmsParam { Key = "SenderName", Value = s.SenderName });
                if (!string.IsNullOrWhiteSpace(s.SoapSendingSystem) && s.SoapSendingSystem != "SCADA")
                    placeholders.Add(new SmsParam { Key = "SendingSystem", Value = s.SoapSendingSystem });
                if (!string.IsNullOrWhiteSpace(s.SoapMessageType) && s.SoapMessageType != "SmsType1")
                    placeholders.Add(new SmsParam { Key = "MessageType", Value = s.SoapMessageType });

                SmsSettingsInput.SoapPlaceholders = placeholders;
                if (placeholders.Any())
                    _logger.LogInformation("Auto-migrated {Count} SOAP placeholders from legacy fields", placeholders.Count);
            }

            // Sync the JSON bind properties so the row editor UI picks them up on GET
            RestHeadersJson      = JsonSerializer.Serialize(SmsSettingsMerge.MaskSecrets(SmsSettingsInput.RestHeaders));
            RestBodyParamsJson   = JsonSerializer.Serialize(SmsSettingsMerge.MaskSecrets(SmsSettingsInput.RestBodyParams));
            SoapPlaceholdersJson = JsonSerializer.Serialize(SmsSettingsMerge.MaskSecrets(SmsSettingsInput.SoapPlaceholders));
        }


        public sealed class ResponseEvaluationInput
        {
            public string ProviderType { get; set; } = "REST";
            public string ResponseBody { get; set; } = "";
            public string SuccessPattern { get; set; } = "";
            public string ErrorPattern { get; set; } = "";
        }

        private async Task LoadSystemInformationAsync()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version?.ToString() ?? "Unknown";
                var buildDate = GetBuildDate(assembly);

                SystemInformation = new SystemInfo
                {
                    Version = version,
                    BuildDate = buildDate,
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    ServerTime = DateTime.Now,
                    Uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime,
                    MachineName = Environment.MachineName,
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSetMemory = Environment.WorkingSet / (1024 * 1024), // MB
                    FrameworkVersion = Environment.Version.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading system information");
                SystemInformation = new SystemInfo();
            }
        }

        private void LoadSmsSettings()
        {
            try
            {
                CurrentSmsSettings = _smsSettings.CurrentValue ?? new SmsSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading SMS settings");
                CurrentSmsSettings = new SmsSettings();
            }
        }

        private async Task LoadHealthStatusAsync()
        {
            try
            {
                var users = await _userService.GetAllUsersAsync();
                var groups = await _groupService.GetAllGroupsAsync();
                var recentAudits = await _auditService.GetRecentAuditHistoryAsync(24);
                var auditList = recentAudits.ToList();
                var successRate = auditList.Any()
                    ? (double)auditList.Count(a => a.Status == "SUCCESS") / auditList.Count * 100
                    : 100.0;

                HealthStatus = new SystemHealthStatus
                {
                    DatabaseConnected = true, // If we got here, DB is connected
                    TotalUsers = users.Count(),
                    ActiveUsers = users.Count(u => u.SmsEnabled),
                    TotalGroups = groups.Count(),
                    ActiveGroups = groups.Count(g => g.GroupMembers?.Any() == true),
                    RecentSmsCount = auditList.Count,
                    SmsSuccessRate = successRate,
                    LastSmsTime = auditList.OrderByDescending(a => a.CreatedAt).FirstOrDefault()?.CreatedAt,
                    SystemHealth = successRate >= 95 ? "Excellent" : 
                                  successRate >= 80 ? "Good" : 
                                  successRate >= 60 ? "Fair" : "Poor"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading health status");
                HealthStatus = new SystemHealthStatus
                {
                    DatabaseConnected = false,
                    SystemHealth = "Error"
                };
            }
        }

        private static DateTime GetBuildDate(Assembly assembly)
        {
            try
            {
                var location = assembly.Location;
                if (System.IO.File.Exists(location))
                {
                    return System.IO.File.GetCreationTime(location);
                }
            }
            catch
            {
                // Ignore errors
            }
            return DateTime.MinValue;
        }
    }

    public class SystemInfo
    {
        public string Version { get; set; } = string.Empty;
        public DateTime BuildDate { get; set; }
        public string Environment { get; set; } = string.Empty;
        public DateTime ServerTime { get; set; }
        public TimeSpan Uptime { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public long WorkingSetMemory { get; set; }
        public string FrameworkVersion { get; set; } = string.Empty;
    }

    public class SystemHealthStatus
    {
        public bool DatabaseConnected { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalGroups { get; set; }
        public int ActiveGroups { get; set; }
        public int RecentSmsCount { get; set; }
        public double SmsSuccessRate { get; set; }
        public DateTime? LastSmsTime { get; set; }
        public string SystemHealth { get; set; } = "Unknown";

        public string GetHealthBadgeClass()
        {
            return SystemHealth.ToLower() switch
            {
                "excellent" => "badge-success",
                "good" => "badge-primary",
                "fair" => "badge-warning",
                "poor" => "badge-danger",
                _ => "badge-secondary"
            };
        }
    }
}
