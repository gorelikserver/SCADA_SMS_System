using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SCADASMSSystem.Web.Pages.Groups
{
    public class CreateModel : PageModel
    {
        private readonly IGroupService _groupService;
        private readonly IUserService _userService;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(IGroupService groupService, IUserService userService, ILogger<CreateModel> logger)
        {
            _groupService = groupService;
            _userService = userService;
            _logger = logger;
        }

        [BindProperty]
        public GroupCreateModel GroupInput { get; set; } = new();

        public IEnumerable<User> AvailableUsers { get; set; } = new List<User>();

        public async Task OnGetAsync()
        {
            await LoadAvailableUsersAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadAvailableUsersAsync();
                return Page();
            }

            try
            {
                // Check if group name already exists
                var existingGroups = await _groupService.GetAllGroupsAsync();
                if (existingGroups.Any(g => g.GroupName.Equals(GroupInput.GroupName, StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError("GroupInput.GroupName", "A group with this name already exists.");
                    await LoadAvailableUsersAsync();
                    return Page();
                }

                // Create group
                var group = new Group
                {
                    GroupName = GroupInput.GroupName,
                    CreatedAt = DateTime.Now
                };

                var success = await _groupService.CreateGroupAsync(group);

                if (success)
                {
                    _logger.LogInformation("Successfully created group {GroupName} with ID {GroupId}", group.GroupName, group.GroupId);
                    
                    // Add initial members if any were selected
                    if (GroupInput.InitialMemberIds != null && GroupInput.InitialMemberIds.Any())
                    {
                        foreach (var userId in GroupInput.InitialMemberIds)
                        {
                            await _groupService.AddUserToGroupAsync(group.GroupId, userId);
                        }
                        _logger.LogInformation("Added {Count} initial members to group {GroupId}", GroupInput.InitialMemberIds.Count(), group.GroupId);
                    }

                    TempData["SuccessMessage"] = $"Group '{group.GroupName}' has been created successfully!";
                    return RedirectToPage("./Index");
                }
                else
                {
                    ModelState.AddModelError("", "Failed to create group. Please try again.");
                    await LoadAvailableUsersAsync();
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group {GroupName}", GroupInput.GroupName);
                ModelState.AddModelError("", "An unexpected error occurred while creating the group.");
                await LoadAvailableUsersAsync();
                return Page();
            }
        }

        private async Task LoadAvailableUsersAsync()
        {
            try
            {
                AvailableUsers = await _userService.GetAllUsersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading available users");
                AvailableUsers = new List<User>();
            }
        }
    }

    public class GroupCreateModel
    {
        [Required]
        [StringLength(255, MinimumLength = 2)]
        [Display(Name = "Group Name")]
        public string GroupName { get; set; } = string.Empty;

        [Display(Name = "Initial Members")]
        public List<int> InitialMemberIds { get; set; } = new();
    }
}