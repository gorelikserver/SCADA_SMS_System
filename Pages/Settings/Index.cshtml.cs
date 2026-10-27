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
        private readonly ISettingsHistory _history;
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
            ISettingsHistory history,
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
            _history = history;
            _logger = logger;
        }

        public SystemInfo SystemInformation { get; set; } = new();
        public SmsSettings CurrentSmsSettings { get; set; } = new();
        public SystemHealthStatus HealthStatus { get; set; } = new();

        [BindProperty]
        public SmsSettings SmsSettingsInput { get; set; } = new();

        // Provider summary for the link card; editing happens on Settings/Provider.
        public ProfileEditorState ProviderState { get; private set; } = new(null, null, null, null);

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading settings page");

                await LoadSystemInformationAsync();
                LoadSmsSettings();
                await LoadHealthStatusAsync();

                SmsSettingsInput = CurrentSmsSettings;
                ProviderState = new ProfileEditorState(CurrentSmsSettings.Profile, CurrentSmsSettings.DraftProfile,
                    CurrentSmsSettings.Verification, CurrentSmsSettings.Activation);

                _logger.LogInformation("Settings page loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings");
                TempData["ErrorMessage"] = "Error loading system settings. Please try again.";
            }
        }

        // Only these fields are edited here; the provider profile has its own page.
        private static readonly string[] GeneralFields =
        {
            nameof(SmsSettings.RateLimit), nameof(SmsSettings.RateWindow), nameof(SmsSettings.DuplicateWindow),
            nameof(SmsSettings.TimeoutSeconds), nameof(SmsSettings.RetryAttempts), nameof(SmsSettings.ScadaPcimObjectId),
            nameof(SmsSettings.TestMode), nameof(SmsSettings.LogSoapXml),
        };

        public async Task<IActionResult> OnPostSaveSettingsAsync()
        {
            var s = SmsSettingsInput;
            var errors = new List<string>();
            if (s.RateLimit is < 1 or > 10000) errors.Add("Rate limit must be 1-10000.");
            if (s.RateWindow is < 1 or > 86400) errors.Add("Rate window must be 1-86400 seconds.");
            if (s.DuplicateWindow is < 0 or > 1440) errors.Add("Duplicate window must be 0-1440 minutes.");
            if (s.TimeoutSeconds is < 3 or > 300) errors.Add("Timeout must be 3-300 seconds.");
            if (s.RetryAttempts is < 0 or > 10) errors.Add("Retry attempts must be 0-10.");
            if (s.ScadaPcimObjectId <= 0) errors.Add("PCIM Object ID must be a positive number.");
            if (errors.Count > 0)
            {
                TempData["ErrorMessage"] = "Settings not saved: " + string.Join(" ", errors);
                return RedirectToPage();
            }

            const string prefix = "SmsSettingsInput.";
            var posted = Request.Form.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .Select(k => k.Substring(prefix.Length))
                .Intersect(GeneralFields, StringComparer.OrdinalIgnoreCase)
                .ToList();

            try
            {
                string? changes = null;
                await _settingsStore.UpdateSmsSettingsAsync(section =>
                {
                    var before = SmsSettingsMerge.ToSettings(section);
                    SmsSettingsMerge.ApplyPostedFields(section, s, posted);
                    changes = SettingsChangeSummary.DiffGeneral(before, SmsSettingsMerge.ToSettings(section), GeneralFields);
                });
                _logger.LogWarning("{User} saved general SMS settings (test mode {TestMode})", User.Identity?.Name, s.TestMode);
                TempData["SuccessMessage"] = "Settings saved. Changes are active immediately.";

                if (changes != null)
                    await _history.RecordChangeAsync(SettingsAreas.General, SettingsActions.GeneralSaved, changes, User.Identity?.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save settings");
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
