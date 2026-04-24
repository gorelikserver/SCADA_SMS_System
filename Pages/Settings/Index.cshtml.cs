using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
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
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IOptionsMonitor<SmsSettings> smsSettings,
            IUserService userService,
            IGroupService groupService,
            IAuditService auditService,
            IHolidayService holidayService,
            IWebHostEnvironment env,
            IConfiguration configuration,
            ILogger<IndexModel> logger)
        {
            _smsSettings = smsSettings;
            _userService = userService;
            _groupService = groupService;
            _auditService = auditService;
            _holidayService = holidayService;
            _env = env;
            _configuration = configuration;
            _logger = logger;
        }

        public SystemInfo SystemInformation { get; set; } = new();
        public SmsSettings CurrentSmsSettings { get; set; } = new();
        public SystemHealthStatus HealthStatus { get; set; } = new();

        [BindProperty]
        public SmsSettings SmsSettingsInput { get; set; } = new();

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
                    username = s.Username ?? "",
                    testMode = s.TestMode
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
            if (!ModelState.IsValid)
            {
                await LoadSystemInformationAsync();
                LoadSmsSettings();
                await LoadHealthStatusAsync();
                return Page();
            }

            try
            {
                var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
                var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                var root = JsonNode.Parse(json)!.AsObject();

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

                // Mandatory dynamic vars: when SOAP provider is active and a template is supplied,
                // the template MUST contain the {Message} placeholder (per SCADA integration guide).
                // Phone is no longer enforced server-side \u2014 admins choose whether their gateway needs
                // a {Phone} token (added via the "+ Add variable" picker in Settings).
                if (string.Equals(SmsSettingsInput.ProviderType, "SOAP", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(SmsSettingsInput.SoapBodyTemplate))
                {
                    var tpl = SmsSettingsInput.SoapBodyTemplate;
                    if (tpl.IndexOf("{Message}", StringComparison.Ordinal) < 0)
                        ModelState.AddModelError("SmsSettingsInput.SoapBodyTemplate",
                            "SOAP body template must include the mandatory {Message} placeholder.");
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

                var smsNode = new JsonObject
                {
                    // Core
                    ["ProviderType"]      = SmsSettingsInput.ProviderType,
                    ["ApiEndpoint"]       = SmsSettingsInput.ApiEndpoint,
                    ["HttpMethod"]        = SmsSettingsInput.HttpMethod,
                    ["ContentType"]       = SmsSettingsInput.ContentType,
                    ["ApiParams"]         = SmsSettingsInput.ApiParams,
                    ["Username"]          = SmsSettingsInput.Username,
                    ["Password"]          = SmsSettingsInput.Password,
                    ["SenderName"]        = SmsSettingsInput.SenderName,
                    ["RateLimit"]         = SmsSettingsInput.RateLimit,
                    ["RateWindow"]        = SmsSettingsInput.RateWindow,
                    ["DuplicateWindow"]   = SmsSettingsInput.DuplicateWindow,
                    ["TimeoutSeconds"]    = SmsSettingsInput.TimeoutSeconds,
                    ["RetryAttempts"]     = SmsSettingsInput.RetryAttempts,
                    ["ScadaPcimObjectId"] = SmsSettingsInput.ScadaPcimObjectId,
                    // REST extras
                    ["ApiHeaders"]          = SmsSettingsInput.ApiHeaders,
                    ["RestAuthType"]        = SmsSettingsInput.RestAuthType,
                    ["RestBearerToken"]     = SmsSettingsInput.RestBearerToken,
                    ["RestApiKeyName"]      = SmsSettingsInput.RestApiKeyName,
                    ["RestApiKeyValue"]     = SmsSettingsInput.RestApiKeyValue,
                    ["RestApiKeyLocation"]  = SmsSettingsInput.RestApiKeyLocation,
                    ["RestSuccessPattern"]  = SmsSettingsInput.RestSuccessPattern,
                    ["RestFailurePattern"]  = SmsSettingsInput.RestFailurePattern,
                    // SOAP
                    ["SoapAction"]          = SmsSettingsInput.SoapAction,
                    ["SoapMessageType"]     = SmsSettingsInput.SoapMessageType,
                    ["SoapSendingSystem"]   = SmsSettingsInput.SoapSendingSystem,
                    ["SoapAuthType"]        = SmsSettingsInput.SoapAuthType,
                    ["SoapBodyTemplate"]    = SmsSettingsInput.SoapBodyTemplate,
                    ["SoapEnvelopeNamespaces"] = SmsSettingsInput.SoapEnvelopeNamespaces,
                    ["SoapSuccessPattern"]  = SmsSettingsInput.SoapSuccessPattern,
                    ["SoapErrorPattern"]    = SmsSettingsInput.SoapErrorPattern,
                    ["SoapParams"]          = SmsSettingsInput.SoapParams,
                    // Test mode
                    ["TestMode"]            = SmsSettingsInput.TestMode
                };

                root["SmsSettings"] = smsNode;

                var writeOptions = new JsonSerializerOptions { WriteIndented = true };
                await System.IO.File.WriteAllTextAsync(appSettingsPath,
                    root.ToJsonString(writeOptions));

                // Force immediate config reload so IOptionsMonitor.CurrentValue reflects new values
                // before the redirect GET request arrives (avoids file-watcher race condition).
                if (_configuration is IConfigurationRoot configRoot)
                    configRoot.Reload();

                _logger.LogInformation("SMS settings saved to appsettings.json by user");
                TempData["SuccessMessage"] = "Settings saved successfully. Changes are active immediately.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings to appsettings.json");
                TempData["ErrorMessage"] = $"Failed to save settings: {ex.Message}";
            }

            return RedirectToPage();
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