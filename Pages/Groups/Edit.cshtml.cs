using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SCADASMSSystem.Web.Pages.Groups
{
    public class EditModel : PageModel
    {
        private readonly IGroupService _groupService;
        private readonly IUserService _userService;
        private readonly ILogger<EditModel> _logger;

        public EditModel(IGroupService groupService, IUserService userService, ILogger<EditModel> logger)
        {
            _groupService = groupService;
            _userService = userService;
            _logger = logger;
        }

        [BindProperty]
        public GroupEditModel GroupInput { get; set; } = new();

        public IEnumerable<User> CurrentMembers { get; set; } = new List<User>();
        public IEnumerable<User> AvailableUsers { get; set; } = new List<User>();
        public DateTime? GroupCreatedAt { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var group = await _groupService.GetGroupByIdAsync(id);
            if (group == null)
            {
                return NotFound();
            }

            GroupInput = new GroupEditModel
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                OriginalGroupName = group.GroupName
            };

            GroupCreatedAt = group.CreatedAt;

            await LoadGroupDataAsync(id);
            
            _logger.LogDebug("Loaded group {GroupId} with {CurrentCount} current members and {AvailableCount} available users",
                id, CurrentMembers.Count(), AvailableUsers.Count());
            
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadGroupDataAsync(GroupInput.GroupId);
                return Page();
            }

            try
            {
                // Check if group name already exists (excluding current group)
                if (GroupInput.GroupName != GroupInput.OriginalGroupName)
                {
                    var existingGroups = await _groupService.GetAllGroupsAsync();
                    if (existingGroups.Any(g => g.GroupId != GroupInput.GroupId && 
                                         g.GroupName.Equals(GroupInput.GroupName, StringComparison.OrdinalIgnoreCase)))
                    {
                        ModelState.AddModelError("GroupInput.GroupName", "A group with this name already exists.");
                        await LoadGroupDataAsync(GroupInput.GroupId);
                        return Page();
                    }
                }

                // Get and update the group
                var group = await _groupService.GetGroupByIdAsync(GroupInput.GroupId);
                if (group == null)
                {
                    return NotFound();
                }

                group.GroupName = GroupInput.GroupName;
                var success = await _groupService.UpdateGroupAsync(group);

                if (success)
                {
                    _logger.LogInformation("Successfully updated group {GroupId} - {GroupName}", group.GroupId, group.GroupName);
                    TempData["SuccessMessage"] = $"Group '{group.GroupName}' has been updated successfully!";
                    return RedirectToPage("./Index");
                }
                else
                {
                    ModelState.AddModelError("", "Failed to update group. Please try again.");
                    await LoadGroupDataAsync(GroupInput.GroupId);
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group {GroupId}", GroupInput.GroupId);
                ModelState.AddModelError("", "An unexpected error occurred while updating the group.");
                await LoadGroupDataAsync(GroupInput.GroupId);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAddMemberAsync(int groupId, int userId)
        {
            try
            {
                var success = await _groupService.AddUserToGroupAsync(groupId, userId);
                if (success)
                {
                    var user = await _userService.GetUserByIdAsync(userId);
                    TempData["SuccessMessage"] = $"Added {user?.UserName} to the group successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to add user to group.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {UserId} to group {GroupId}", userId, groupId);
                TempData["ErrorMessage"] = "An error occurred while adding the user to the group.";
            }

            return RedirectToPage(new { id = groupId });
        }

        public async Task<IActionResult> OnPostRemoveMemberAsync(int groupId, int userId)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                var success = await _groupService.RemoveUserFromGroupAsync(groupId, userId);
                
                if (success)
                {
                    TempData["SuccessMessage"] = $"Removed {user?.UserName} from the group successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to remove user from group.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {UserId} from group {GroupId}", userId, groupId);
                TempData["ErrorMessage"] = "An error occurred while removing the user from the group.";
            }

            return RedirectToPage(new { id = groupId });
        }

        private async Task LoadGroupDataAsync(int groupId)
        {
            try
            {
                _logger.LogInformation("=== LOADING GROUP DATA FOR GROUP {GroupId} ===", groupId);
                
                CurrentMembers = await _groupService.GetGroupMembersAsync(groupId);
                _logger.LogInformation("? Found {Count} current members: [{Members}]", 
                    CurrentMembers.Count(), 
                    string.Join(", ", CurrentMembers.Select(u => $"{u.UserName} (ID:{u.UserId})")));
                
                AvailableUsers = await _groupService.GetAvailableUsersForGroupAsync(groupId);
                _logger.LogInformation("? Found {Count} available users: [{AvailableUsers}]", 
                    AvailableUsers.Count(), 
                    string.Join(", ", AvailableUsers.Select(u => $"{u.UserName} (ID:{u.UserId})")));
                
                // Additional debugging - check total users in system
                var allUsers = await _userService.GetAllUsersAsync();
                _logger.LogInformation("? Total users in system: {TotalUsers} [{AllUsers}]", 
                    allUsers.Count(),
                    string.Join(", ", allUsers.Select(u => $"{u.UserName} (ID:{u.UserId})")));

                // Manual calculation check
                var currentMemberIds = CurrentMembers.Select(u => u.UserId).ToList();
                var manualAvailable = allUsers.Where(u => !currentMemberIds.Contains(u.UserId)).ToList();
                _logger.LogInformation("? Manual available calculation: {Count} [{ManualAvailable}]", 
                    manualAvailable.Count(),
                    string.Join(", ", manualAvailable.Select(u => $"{u.UserName} (ID:{u.UserId})")));

                if (AvailableUsers.Count() != manualAvailable.Count())
                {
                    _logger.LogWarning("?? MISMATCH: Service returned {ServiceCount} available users but manual calculation shows {ManualCount}",
                        AvailableUsers.Count(), manualAvailable.Count());
                }

                _logger.LogInformation("=== GROUP DATA LOADING COMPLETE ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "? Error loading group data for group {GroupId}", groupId);
                CurrentMembers = new List<User>();
                AvailableUsers = new List<User>();
            }
        }
    }

    public class GroupEditModel
    {
        public int GroupId { get; set; }

        [Required]
        [StringLength(255, MinimumLength = 2)]
        [Display(Name = "Group Name")]
        public string GroupName { get; set; } = string.Empty;

        public string OriginalGroupName { get; set; } = string.Empty;
    }
}