using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly SmsBackgroundService _smsBackgroundService;
    private readonly IUserService _userService;
    private readonly IGroupService _groupService;

    public IndexModel(
        ILogger<IndexModel> logger, 
        SmsBackgroundService smsBackgroundService,
        IUserService userService,
        IGroupService groupService)
    {
        _logger = logger;
        _smsBackgroundService = smsBackgroundService;
        _userService = userService;
        _groupService = groupService;
    }

    public DashboardData Dashboard { get; set; } = new();

    public async Task OnGetAsync()
    {
        try
        {
            // Get SMS service status
            var smsStatus = _smsBackgroundService.GetServiceStatus();
            
            // Get user and group counts
            var users = await _userService.GetAllUsersAsync();
            var groups = await _groupService.GetAllGroupsAsync();

            Dashboard = new DashboardData
            {
                SmsServiceStatus = smsStatus,
                TotalUsers = users.Count(),
                ActiveUsers = users.Count(u => u.SmsEnabled),
                TotalGroups = groups.Count(),
                ActiveGroups = groups.Count(g => g.GroupMembers?.Any() == true)
            };

            _logger.LogInformation("Dashboard loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard data");
            Dashboard = new DashboardData(); // Initialize with defaults
        }
    }

    // API endpoint for real-time dashboard updates
    public async Task<IActionResult> OnGetRefreshDataAsync()
    {
        try
        {
            var smsStatus = _smsBackgroundService.GetServiceStatus();
            var users = await _userService.GetAllUsersAsync();
            var groups = await _groupService.GetAllGroupsAsync();

            var data = new
            {
                smsService = new
                {
                    isHealthy = smsStatus.IsHealthy,
                    queueSize = smsStatus.QueueSize,
                    messagesSent = smsStatus.MessagesSent,
                    messagesFailed = smsStatus.MessagesFailed,
                    uptime = smsStatus.ServiceUptime.ToString(@"d\.hh\:mm\:ss"),
                    lastMessageTime = smsStatus.LastMessageTime?.ToString("HH:mm:ss"),
                    rateLimit = smsStatus.RateLimit,
                    deduplicationEnabled = smsStatus.DeduplicationEnabled
                },
                stats = new
                {
                    totalUsers = users.Count(),
                    activeUsers = users.Count(u => u.SmsEnabled),
                    totalGroups = groups.Count(),
                    activeGroups = groups.Count(g => g.GroupMembers?.Any() == true)
                }
            };

            return new JsonResult(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing dashboard data");
            return BadRequest("Error refreshing data");
        }
    }

    // API endpoint for recent activity
    public async Task<IActionResult> OnGetRecentActivityAsync()
    {
        try
        {
            var auditService = HttpContext.RequestServices.GetService<IAuditService>();
            if (auditService == null)
            {
                return new JsonResult(new { activities = new object[0] });
            }

            var recentAudits = await auditService.GetAuditHistoryAsync(1, 10);
            
            var activities = recentAudits.Select(audit => new
            {
                time = audit.CreatedAt.ToString("HH:mm:ss"),
                action = $"SMS to {audit.PhoneNumber}",
                user = audit.User?.UserName ?? "Unknown User",
                status = audit.Status,
                statusClass = audit.Status.ToUpper() switch
                {
                    "SUCCESS" => "badge bg-success text-white",
                    "SENT" => "badge bg-success text-white", // Legacy support 
                    "FAILED" => "badge bg-danger text-white",
                    "PENDING" => "badge bg-warning text-dark",
                    _ => "badge bg-secondary text-white"
                },
                alarmId = audit.AlarmId
            }).ToList();

            return new JsonResult(new { activities });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent activity");
            return BadRequest("Error getting recent activity");
        }
    }
}

public class DashboardData
{
    public SmsServiceStatus SmsServiceStatus { get; set; } = new();
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalGroups { get; set; }
    public int ActiveGroups { get; set; }
}
