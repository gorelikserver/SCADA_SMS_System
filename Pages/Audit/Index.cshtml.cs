using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Audit
{
    public class IndexModel : PageModel
    {
        private readonly IAuditService _auditService;
        private readonly IUserService _userService;
        private readonly IGroupService _groupService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IAuditService auditService, IUserService userService, IGroupService groupService, ILogger<IndexModel> logger)
        {
            _auditService = auditService;
            _userService = userService;
            _groupService = groupService;
            _logger = logger;
        }

        public IEnumerable<SmsAuditWithUserInfo> AuditRecords { get; set; } = new List<SmsAuditWithUserInfo>();
        public IEnumerable<User> AvailableUsers { get; set; } = new List<User>();
        public IEnumerable<Group> AvailableGroups { get; set; } = new List<Group>();
        
        // NEW: All filtered records for export (not paginated)
        public IEnumerable<SmsAuditWithUserInfo> AllFilteredRecords { get; set; } = new List<SmsAuditWithUserInfo>();
        
        // Search and Filter Properties
        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = string.Empty;
        
        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = string.Empty;
        
        [BindProperty(SupportsGet = true)]
        public int? UserIdFilter { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public int? GroupIdFilter { get; set; }

        // ? NEW: Sorting Properties
        [BindProperty(SupportsGet = true)]
        public string SortColumn { get; set; } = "CreatedAt";
        
        [BindProperty(SupportsGet = true)]
        public string SortDirection { get; set; } = "desc";

        // ? NEW: Pagination Properties
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;
        
        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 25; // Changed from 50 to 25

        // ? NEW: Display Properties
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading audit page - Search: '{SearchTerm}', Status: '{StatusFilter}', User: {UserId}, Sort: {SortColumn} {SortDirection}, Page: {PageNumber}", 
                    SearchTerm, StatusFilter, UserIdFilter, SortColumn, SortDirection, PageNumber);

                // Set default date range if not specified
                StartDate ??= DateTime.Now.AddDays(-7);
                EndDate ??= DateTime.Now.AddDays(1);

                // Load reference data
                await LoadReferenceDataAsync();

                // Get and process audit records
                await LoadAuditRecordsAsync();

                _logger.LogInformation("Loaded {Count} audit records (Page {PageNumber} of {TotalPages})", 
                    AuditRecords.Count(), PageNumber, TotalPages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading audit records");
                AuditRecords = new List<SmsAuditWithUserInfo>();
                TempData["ErrorMessage"] = "Error loading audit records. Please try again.";
            }
        }

        private async Task LoadAuditRecordsAsync()
        {
            // Get ALL audit records (no pagination at service level - we handle it in the page model)
            // The service already includes User and Group via .Include(), so no need for separate queries
            var allAuditRecords = await _auditService.GetAuditHistoryAsync(page: 1, pageSize: int.MaxValue);
            
            // Convert to our display model - User and Group are already loaded by EF Core
            var auditRecordsWithUserInfo = allAuditRecords.Select(audit => new SmsAuditWithUserInfo
            {
                Audit = audit,
                UserName = audit.User?.UserName ?? "Unknown User",
                UserPhone = audit.PhoneNumber,
                GroupName = audit.Group?.GroupName ?? "No Group"
            }).ToList();

            // Apply filters first
            var filteredRecords = ApplyFilters(auditRecordsWithUserInfo);
            
            // Get total count for pagination
            TotalRecords = filteredRecords.Count();

            // Apply sorting
            var sortedRecords = ApplySorting(filteredRecords);

            // IMPORTANT: Store ALL filtered records for export (before pagination)
            AllFilteredRecords = sortedRecords.ToList();

            // Apply pagination for display
            AuditRecords = ApplyPagination(sortedRecords);
        }

        private IEnumerable<SmsAuditWithUserInfo> ApplyFilters(IEnumerable<SmsAuditWithUserInfo> records)
        {
            var filtered = records;

            // Date range filter - DEFENSIVE: Handle null audits safely
            if (StartDate.HasValue)
            {
                var startDateValue = StartDate.Value.Date;  // Get date part only
                filtered = filtered.Where(a => a.Audit != null && a.Audit.CreatedAt >= startDateValue);
            }
            
            if (EndDate.HasValue)
            {
                var endDateValue = EndDate.Value.Date.AddDays(1).AddTicks(-1);  // End of day
                filtered = filtered.Where(a => a.Audit != null && a.Audit.CreatedAt <= endDateValue);
            }

            // Search term filter (enhanced) - DEFENSIVE: Add null checks
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                filtered = filtered.Where(a => 
                    a.Audit != null && (
                        (a.Audit.AlarmDescription != null && a.Audit.AlarmDescription.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (a.Audit.AlarmId != null && a.Audit.AlarmId.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (a.UserName != null && a.UserName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (a.Audit.PhoneNumber != null && a.Audit.PhoneNumber.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (a.Audit.MessageStatus != null && a.Audit.MessageStatus.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase))
                    )
                );
            }

            // Status filter - DEFENSIVE: Add null check
            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                filtered = filtered.Where(a => 
                    a.Audit != null && a.Audit.Status != null && a.Audit.Status.Equals(StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            // User filter
            if (UserIdFilter.HasValue && UserIdFilter.Value > 0)
            {
                filtered = filtered.Where(a => a.Audit != null && a.Audit.UserId == UserIdFilter.Value);
            }

            // Group filter
            if (GroupIdFilter.HasValue && GroupIdFilter.Value > 0)
            {
                filtered = filtered.Where(a => a.Audit != null && a.Audit.GroupId.HasValue && a.Audit.GroupId.Value == GroupIdFilter.Value);
            }

            return filtered;
        }

        // ? NEW: Advanced Sorting Implementation
        private IEnumerable<SmsAuditWithUserInfo> ApplySorting(IEnumerable<SmsAuditWithUserInfo> records)
        {
            var isDescending = SortDirection?.ToLower() == "desc";
            
            return SortColumn?.ToLower() switch
            {
                "createdat" or "timestamp" => isDescending 
                    ? records.OrderByDescending(a => a.Audit.CreatedAt)
                    : records.OrderBy(a => a.Audit.CreatedAt),
                
                "alarmid" => isDescending 
                    ? records.OrderByDescending(a => a.Audit.AlarmId)
                    : records.OrderBy(a => a.Audit.AlarmId),
                
                "description" or "alarmdescription" => isDescending 
                    ? records.OrderByDescending(a => a.Audit.AlarmDescription)
                    : records.OrderBy(a => a.Audit.AlarmDescription),
                
                "user" or "username" => isDescending 
                    ? records.OrderByDescending(a => a.UserName)
                    : records.OrderBy(a => a.UserName),
                
                "group" or "groupname" => isDescending 
                    ? records.OrderByDescending(a => a.GroupName)
                    : records.OrderBy(a => a.GroupName),
                
                "phone" or "phonenumber" => isDescending 
                    ? records.OrderByDescending(a => a.Audit.PhoneNumber)
                    : records.OrderBy(a => a.Audit.PhoneNumber),
                
                "status" => isDescending 
                    ? records.OrderByDescending(a => a.Audit.Status)
                    : records.OrderBy(a => a.Audit.Status),
                
                "response" or "messagestatus" => isDescending 
                    ? records.OrderByDescending(a => a.Audit.MessageStatus ?? "")
                    : records.OrderBy(a => a.Audit.MessageStatus ?? ""),
                
                _ => records.OrderByDescending(a => a.Audit.CreatedAt) // Default: newest first
            };
        }

        // ? NEW: Pagination Implementation
        private IEnumerable<SmsAuditWithUserInfo> ApplyPagination(IEnumerable<SmsAuditWithUserInfo> records)
        {
            return records
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize);
        }

        // ? NEW: Helper method for generating sort URLs
        public string GetSortUrl(string column)
        {
            var newDirection = (SortColumn == column && SortDirection == "asc") ? "desc" : "asc";
            var queryParams = new Dictionary<string, string?>
            {
                ["SortColumn"] = column,
                ["SortDirection"] = newDirection,
                ["PageNumber"] = "1", // Reset to first page when sorting
                ["SearchTerm"] = SearchTerm,
                ["StatusFilter"] = StatusFilter,
                ["UserIdFilter"] = UserIdFilter?.ToString(),
                ["GroupIdFilter"] = GroupIdFilter?.ToString(),
                ["StartDate"] = StartDate?.ToString("yyyy-MM-dd"),
                ["EndDate"] = EndDate?.ToString("yyyy-MM-dd"),
                ["PageSize"] = PageSize.ToString()
            };

            var query = string.Join("&", queryParams
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                .Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value!)}"));

            return $"/Audit?{query}";
        }

        // ? NEW: Helper method for generating pagination URLs  
        public string GetPageUrl(int pageNumber)
        {
            var queryParams = new Dictionary<string, string?>
            {
                ["PageNumber"] = pageNumber.ToString(),
                ["SortColumn"] = SortColumn,
                ["SortDirection"] = SortDirection,
                ["SearchTerm"] = SearchTerm,
                ["StatusFilter"] = StatusFilter,
                ["UserIdFilter"] = UserIdFilter?.ToString(),
                ["GroupIdFilter"] = GroupIdFilter?.ToString(),
                ["StartDate"] = StartDate?.ToString("yyyy-MM-dd"),
                ["EndDate"] = EndDate?.ToString("yyyy-MM-dd"),
                ["PageSize"] = PageSize.ToString()
            };

            var query = string.Join("&", queryParams
                .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                .Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value!)}"));

            return $"/Audit?{query}";
        }

        // ? NEW: Get sort icon for column headers
        public string GetSortIcon(string column)
        {
            if (SortColumn != column)
                return "fas fa-sort text-muted";
            
            return SortDirection == "asc" 
                ? "fas fa-sort-up text-primary" 
                : "fas fa-sort-down text-primary";
        }

        private async Task LoadReferenceDataAsync()
        {
            try
            {
                AvailableUsers = await _userService.GetAllUsersAsync();
                AvailableGroups = await _groupService.GetAllGroupsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reference data");
                AvailableUsers = new List<User>();
                AvailableGroups = new List<Group>();
            }
        }

        public async Task<IActionResult> OnPostCleanupAsync()
        {
            try
            {
                var success = await _auditService.CleanupOldAuditsAsync(90); // Keep 90 days

                if (success)
                {
                    TempData["SuccessMessage"] = "Old audit records have been cleaned up successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to cleanup old audit records.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up audit records");
                TempData["ErrorMessage"] = "An error occurred while cleaning up audit records.";
            }

            return RedirectToPage();
        }
    }

    public class SmsAuditWithUserInfo
    {
        public SmsAudit Audit { get; set; } = new();
        public string UserName { get; set; } = string.Empty;
        public string UserPhone { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
    }
}