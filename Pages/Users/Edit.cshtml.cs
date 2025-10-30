using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SCADASMSSystem.Web.Pages.Users
{
    public class EditModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILogger<EditModel> _logger;

        public EditModel(IUserService userService, ILogger<EditModel> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [BindProperty]
        public UserEditModel UserInput { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            UserInput = new UserEditModel
            {
                UserId = user.UserId,
                UserName = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                SmsEnabled = user.SmsEnabled,
                SpecialDaysEnabled = user.SpecialDaysEnabled,
                OriginalEmail = user.Email,
                OriginalPhoneNumber = user.PhoneNumber
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Check if email exists (only if email provided and changed)
                var existingUsers = await _userService.GetAllUsersAsync();
                
                if (!string.IsNullOrWhiteSpace(UserInput.Email) &&
                    !string.Equals(UserInput.Email, UserInput.OriginalEmail, StringComparison.OrdinalIgnoreCase) &&
                    existingUsers.Any(u => u.UserId != UserInput.UserId && 
                                     !string.IsNullOrWhiteSpace(u.Email) &&
                                     u.Email.Equals(UserInput.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError("UserInput.Email", "A user with this email address already exists.");
                    return Page();
                }

                if (UserInput.PhoneNumber != UserInput.OriginalPhoneNumber && 
                    existingUsers.Any(u => u.UserId != UserInput.UserId && u.PhoneNumber == UserInput.PhoneNumber))
                {
                    ModelState.AddModelError("UserInput.PhoneNumber", "A user with this phone number already exists.");
                    return Page();
                }

                // Get the existing user
                var existingUser = await _userService.GetUserByIdAsync(UserInput.UserId);
                if (existingUser == null)
                {
                    return NotFound();
                }

                // Update user properties
                existingUser.UserName = UserInput.UserName;
                existingUser.Email = string.IsNullOrWhiteSpace(UserInput.Email) ? null : UserInput.Email;
                existingUser.PhoneNumber = UserInput.PhoneNumber;
                existingUser.SmsEnabled = UserInput.SmsEnabled;
                existingUser.SpecialDaysEnabled = UserInput.SpecialDaysEnabled;

                var success = await _userService.UpdateUserAsync(existingUser);

                if (success)
                {
                    _logger.LogInformation("Successfully updated user {UserId} - {UserName}", existingUser.UserId, existingUser.UserName);
                    TempData["SuccessMessage"] = $"User '{existingUser.UserName}' has been updated successfully!";
                    return RedirectToPage("./Index");
                }
                else
                {
                    ModelState.AddModelError("", "Failed to update user. Please check the information and try again.");
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", UserInput.UserId);
                ModelState.AddModelError("", "An unexpected error occurred while updating the user.");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    return NotFound();
                }

                var success = await _userService.DeleteUserAsync(id);

                if (success)
                {
                    _logger.LogInformation("Successfully deleted user {UserId} - {UserName}", id, user.UserName);
                    TempData["SuccessMessage"] = $"User '{user.UserName}' has been deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to delete user. Please try again.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the user.";
            }

            return RedirectToPage("./Index");
        }
    }

    public class UserEditModel
    {
        public int UserId { get; set; }

        [Required]
        [StringLength(255, MinimumLength = 2)]
        [Display(Name = "Full Name")]
        public string UserName { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(255)]
        [Display(Name = "Email Address (Optional)")]
        public string? Email { get; set; }

        [Required]
        [Phone]
        [StringLength(20)]
        [Display(Name = "Phone Number")]
        [RegularExpression(@"^\+?[\d\s\-\(\)]+$", ErrorMessage = "Please enter a valid phone number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Enable SMS Notifications")]
        public bool SmsEnabled { get; set; }

        [Display(Name = "Available on Special Days/Holidays")]
        public bool SpecialDaysEnabled { get; set; }

        // Hidden fields for validation
        public string? OriginalEmail { get; set; }
        public string OriginalPhoneNumber { get; set; } = string.Empty;
    }
}