using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Alarms
{
    public class ManageGroupsModel : PageModel
    {
        private readonly IAlarmActionService _alarmActionService;
        private readonly IGroupService _groupService;
        private readonly ILogger<ManageGroupsModel> _logger;

        public ManageGroupsModel(
            IAlarmActionService alarmActionService,
            IGroupService groupService,
            ILogger<ManageGroupsModel> logger)
        {
            _alarmActionService = alarmActionService;
            _groupService = groupService;
            _logger = logger;
        }

        public IEnumerable<AlarmGroupAssignment> AlarmAssignments { get; set; } = new List<AlarmGroupAssignment>();
        public IEnumerable<Group> AllGroups { get; set; } = new List<Group>();
        public string SearchTerm { get; set; } = string.Empty;
        
        // Pagination properties
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public async Task<IActionResult> OnGetAsync(string? searchTerm = null, int page = 1, int pageSize = 25)
        {
            try
            {
                SearchTerm = searchTerm ?? string.Empty;
                CurrentPage = page > 0 ? page : 1;
                PageSize = pageSize > 0 ? pageSize : 25;

                // Load all alarms
                var alarms = await _alarmActionService.GetAllAlarmActionsAsync();
                
                // Load all groups for the dropdown
                AllGroups = await _groupService.GetAllGroupsAsync();

                // Load all groups lookup for fast access
                var groupsLookup = AllGroups.ToDictionary(g => g.GroupId);

                // Build assignments directly
                var allAssignments = alarms.Select(alarm => new AlarmGroupAssignment
                {
                    BlockId = alarm.BlockId,
                    BlockName = alarm.BlockName,
                    AlarmId = alarm.AlarmId,
                    AlarmDescription = alarm.AlarmDescription,
                    OriginalAction = alarm.Action,
                    AssignedGroupIds = alarm.AssignedGroupIds,
                    IsActive = alarm.IsActive,
                    AssignedGroups = alarm.AssignedGroupIds
                        .Where(gid => groupsLookup.ContainsKey(gid))
                        .Select(gid => groupsLookup[gid])
                        .ToList(),
                    LastModified = alarm.LastModified,
                    ModifiedBy = alarm.ModifiedBy
                }).ToList();

                // Debug: Log group assignments
                var assignmentsWithGroups = allAssignments.Where(a => a.AssignedGroups.Any()).Count();
                _logger.LogInformation("Built {Total} alarm assignments, {WithGroups} have groups assigned", 
                    allAssignments.Count, assignmentsWithGroups);

                // Apply search filter if provided
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    allAssignments = allAssignments.Where(a =>
                        a.BlockName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        (a.AlarmId?.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (a.AlarmDescription?.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
                    ).ToList();
                }

                TotalItems = allAssignments.Count;

                // Apply pagination
                AlarmAssignments = allAssignments
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();

                _logger.LogInformation("Loaded {Count} alarm assignments on page {Page} of {TotalPages} (filtered from {Total} total alarms)", 
                    AlarmAssignments.Count(), CurrentPage, TotalPages, alarms.Count());

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading alarm group management page");
                TempData["ErrorMessage"] = "An error occurred while loading alarm data.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAddGroupAsync(int blockId, int groupId)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var modifiedBy = "System User"; // TODO: Replace with actual authenticated user

                var success = await _alarmActionService.AddGroupToAlarmAsync(blockId, groupId, modifiedBy, ipAddress);

                if (success)
                {
                    var group = await _groupService.GetGroupByIdAsync(groupId);
                    TempData["SuccessMessage"] = $"Successfully added group '{group?.GroupName}' to the alarm.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to add group to alarm. It may already be assigned.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding group {GroupId} to alarm {BlockId}", groupId, blockId);
                TempData["ErrorMessage"] = "An error occurred while adding the group.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveGroupAsync(int blockId, int groupId)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var modifiedBy = "System User"; // TODO: Replace with actual authenticated user

                var group = await _groupService.GetGroupByIdAsync(groupId);
                var success = await _alarmActionService.RemoveGroupFromAlarmAsync(blockId, groupId, modifiedBy, ipAddress);

                if (success)
                {
                    TempData["SuccessMessage"] = $"Successfully removed group '{group?.GroupName}' from the alarm.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to remove group from alarm.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing group {GroupId} from alarm {BlockId}", groupId, blockId);
                TempData["ErrorMessage"] = "An error occurred while removing the group.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostBulkUpdateAsync(int blockId, string? selectedGroups, int page = 1, int pageSize = 25)
        {
            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var modifiedBy = "System User"; // TODO: Replace with actual authenticated user

                _logger.LogInformation("Bulk update started for block {BlockId}", blockId);

                // Parse selected group IDs (supports multiple groups OR zero groups to remove all)
                var groupIds = new List<int>();
                if (!string.IsNullOrWhiteSpace(selectedGroups))
                {
                    groupIds = selectedGroups.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(id => int.TryParse(id.Trim(), out var gid) ? gid : 0)
                        .Where(gid => gid > 0)
                        .ToList();
                }

                // Allow zero groups - this removes all SMS notifications
                if (!groupIds.Any())
                {
                    _logger.LogInformation("Removing all SMS groups from block {BlockId}", blockId);
                    var removeSuccess = await _alarmActionService.RemoveAllGroupsFromAlarmAsync(blockId, modifiedBy, ipAddress);
                    
                    if (removeSuccess)
                    {
                        TempData["SuccessMessage"] = "Successfully removed all SMS group assignments. The alarm will not send SMS notifications.";
                        _logger.LogInformation("Successfully removed all groups from block {BlockId}", blockId);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to remove SMS group assignments.";
                        _logger.LogError("Failed to remove all groups from block {BlockId}", blockId);
                    }
                    
                    return RedirectToPage("/Alarms/ManageGroups", new { searchTerm = SearchTerm, page, pageSize });
                }

                // Normal update with one or more groups
                var success = await _alarmActionService.UpdateAlarmGroupsAsync(blockId, groupIds, modifiedBy, ipAddress);

                if (success)
                {
                    var groups = await _groupService.GetAllGroupsAsync();
                    var selectedGroupNames = groups
                        .Where(g => groupIds.Contains(g.GroupId))
                        .Select(g => g.GroupName)
                        .ToList();
                    
                    var groupsText = string.Join(", ", selectedGroupNames);
                    TempData["SuccessMessage"] = $"Successfully assigned {groupIds.Count} group(s) to the alarm: {groupsText}";
                    _logger.LogInformation("Successfully updated block {BlockId} with {Count} groups", blockId, groupIds.Count);
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update alarm group assignments.";
                    _logger.LogError("Failed to update block {BlockId}", blockId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating groups for alarm {BlockId}", blockId);
                TempData["ErrorMessage"] = "An error occurred while updating the group assignments.";
            }

            // Redirect back with search term and pagination to avoid reloading
            return RedirectToPage("/Alarms/ManageGroups", new { searchTerm = SearchTerm, page, pageSize });
        }
    }
}
