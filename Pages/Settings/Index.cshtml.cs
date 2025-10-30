using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using System.Reflection;

namespace SCADASMSSystem.Web.Pages.Settings
{
    public class IndexModel : PageModel
    {
        private readonly IOptions<SmsSettings> _smsSettings;
        private readonly IUserService _userService;
        private readonly IGroupService _groupService;
        private readonly IAuditService _auditService;
        private readonly IHolidayService _holidayService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IOptions<SmsSettings> smsSettings,
            IUserService userService,
            IGroupService groupService,
            IAuditService auditService,
            IHolidayService holidayService,
            ILogger<IndexModel> logger)
        {
            _smsSettings = smsSettings;
            _userService = userService;
            _groupService = groupService;
            _auditService = auditService;
            _holidayService = holidayService;
            _logger = logger;
        }

        public SystemInfo SystemInformation { get; set; } = new();
        public SmsSettings CurrentSmsSettings { get; set; } = new();
        public SystemHealthStatus HealthStatus { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading settings page");

                await LoadSystemInformationAsync();
                LoadSmsSettings();
                await LoadHealthStatusAsync();

                _logger.LogInformation("Settings page loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings");
                TempData["ErrorMessage"] = "Error loading system settings. Please try again.";
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
                CurrentSmsSettings = _smsSettings.Value ?? new SmsSettings();
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
                var auditRecords = await _auditService.GetAuditHistoryAsync();

                // Check recent audit records for health status
                var recentAudits = auditRecords.Where(a => a.CreatedAt >= DateTime.Now.AddHours(-24));
                var successRate = recentAudits.Any() ? 
                    (double)recentAudits.Count(a => a.Status == "SUCCESS") / recentAudits.Count() * 100 : 100;

                HealthStatus = new SystemHealthStatus
                {
                    DatabaseConnected = true, // If we got here, DB is connected
                    TotalUsers = users.Count(),
                    ActiveUsers = users.Count(u => u.SmsEnabled),
                    TotalGroups = groups.Count(),
                    ActiveGroups = groups.Count(g => g.GroupMembers?.Any() == true),
                    RecentSmsCount = recentAudits.Count(),
                    SmsSuccessRate = successRate,
                    LastSmsTime = auditRecords.OrderByDescending(a => a.CreatedAt).FirstOrDefault()?.CreatedAt,
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