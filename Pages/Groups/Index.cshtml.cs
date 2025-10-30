using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Groups
{
    public class IndexModel : PageModel
    {
        private readonly IGroupService _groupService;
        private readonly IUserService _userService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IGroupService groupService, IUserService userService, ILogger<IndexModel> logger)
        {
            _groupService = groupService;
            _userService = userService;
            _logger = logger;
        }

        public IEnumerable<GroupWithMemberCount> Groups { get; set; } = new List<GroupWithMemberCount>();
        
        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = string.Empty;
        
        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading groups page with search: '{SearchTerm}', status: '{StatusFilter}'", 
                    SearchTerm, StatusFilter);

                var allGroups = await _groupService.GetAllGroupsAsync();
                var groupsWithCounts = new List<GroupWithMemberCount>();

                foreach (var group in allGroups)
                {
                    // Use the existing group members from the Include()
                    var memberCount = group.GroupMembers?.Count() ?? 0;
                    
                    groupsWithCounts.Add(new GroupWithMemberCount
                    {
                        Group = group,
                        MemberCount = memberCount,
                        Status = memberCount > 0 ? "Active" : "Empty",
                        LastActivity = group.CreatedAt // Use creation date as fallback
                    });
                }

                Groups = groupsWithCounts;

                // Apply filters
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    Groups = Groups.Where(g => 
                        g.Group.GroupName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(StatusFilter))
                {
                    Groups = StatusFilter.ToLower() switch
                    {
                        "active" => Groups.Where(g => g.MemberCount > 0),
                        "empty" => Groups.Where(g => g.MemberCount == 0),
                        _ => Groups
                    };
                }

                Groups = Groups.OrderBy(g => g.Group.GroupName);

                _logger.LogInformation("Loaded {Count} groups after filtering", Groups.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading groups");
                Groups = new List<GroupWithMemberCount>();
                TempData["ErrorMessage"] = "Error loading groups. Please try again.";
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int groupId)
        {
            try
            {
                var group = await _groupService.GetGroupByIdAsync(groupId);
                if (group == null)
                {
                    return NotFound();
                }

                var success = await _groupService.DeleteGroupAsync(groupId);

                if (success)
                {
                    _logger.LogInformation("Successfully deleted group {GroupId} - {GroupName}", groupId, group.GroupName);
                    TempData["SuccessMessage"] = $"Group '{group.GroupName}' has been deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete group. Please try again.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group {GroupId}", groupId);
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the group.";
            }

            return RedirectToPage();
        }
    }

    public class GroupWithMemberCount
    {
        public Group Group { get; set; } = new();
        public int MemberCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastActivity { get; set; } = DateTime.Now;
    }
}