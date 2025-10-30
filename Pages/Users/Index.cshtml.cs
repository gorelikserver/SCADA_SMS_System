using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Users
{
    public class IndexModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IUserService userService, ILogger<IndexModel> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public IEnumerable<User> Users { get; set; } = new List<User>();
        
        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = string.Empty;
        
        [BindProperty(SupportsGet = true)]
        public bool? SmsEnabledFilter { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Loading users page with search: '{SearchTerm}', sms: {SmsEnabled}", 
                    SearchTerm, SmsEnabledFilter);

                Users = await _userService.GetAllUsersAsync();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    Users = Users.Where(u => 
                        u.UserName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        u.Email.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                        u.PhoneNumber.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase));
                }

                if (SmsEnabledFilter.HasValue)
                {
                    Users = Users.Where(u => u.SmsEnabled == SmsEnabledFilter.Value);
                }

                _logger.LogInformation("Loaded {Count} users after filtering", Users.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");
                Users = new List<User>();
                TempData["ErrorMessage"] = "Error loading users. Please try again.";
            }
        }

        public async Task<IActionResult> OnPostToggleSmsAsync(int userId)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                var newValue = !user.SmsEnabled;
                var success = await _userService.ToggleUserSettingAsync(userId, "sms_enabled", newValue);

                if (success)
                {
                    TempData["SuccessMessage"] = $"SMS notifications {(newValue ? "enabled" : "disabled")} for {user.UserName}";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update SMS setting";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling SMS for user {UserId}", userId);
                TempData["ErrorMessage"] = "Error updating SMS setting";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleHolidayAsync(int userId)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                var newValue = !user.SpecialDaysEnabled;
                var success = await _userService.ToggleUserSettingAsync(userId, "special_days_enabled", newValue);

                if (success)
                {
                    TempData["SuccessMessage"] = $"Holiday work setting {(newValue ? "enabled" : "disabled")} for {user.UserName}";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update holiday setting";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling holiday setting for user {UserId}", userId);
                TempData["ErrorMessage"] = "Error updating holiday setting";
            }

            return RedirectToPage();
        }
    }
}