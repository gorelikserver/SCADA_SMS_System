using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SCADASMSSystem.Web.Pages.Users
{
    public class CreateModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(IUserService userService, ILogger<CreateModel> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [BindProperty]
        public UserCreateModel UserInput { get; set; } = new();

        public void OnGet()
        {
            // Initialize with default values
            UserInput.SmsEnabled = true;
            UserInput.SpecialDaysEnabled = false;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Check if email exists (only if email provided)
                var existingUsers = await _userService.GetAllUsersAsync();
                
                if (!string.IsNullOrWhiteSpace(UserInput.Email) &&
                    existingUsers.Any(u => !string.IsNullOrWhiteSpace(u.Email) && 
                                     u.Email.Equals(UserInput.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError("UserInput.Email", "A user with this email address already exists.");
                    return Page();
                }

                if (existingUsers.Any(u => u.PhoneNumber == UserInput.PhoneNumber))
                {
                    ModelState.AddModelError("UserInput.PhoneNumber", "A user with this phone number already exists.");
                    return Page();
                }

                // Create user object
                var user = new User
                {
                    UserName = UserInput.UserName,
                    Email = string.IsNullOrWhiteSpace(UserInput.Email) ? null : UserInput.Email,
                    PhoneNumber = UserInput.PhoneNumber,
                    SmsEnabled = UserInput.SmsEnabled,
                    SpecialDaysEnabled = UserInput.SpecialDaysEnabled,
                    CreatedAt = DateTime.Now
                };

                var success = await _userService.CreateUserAsync(user);

                if (success)
                {
                    _logger.LogInformation("Successfully created user {UserName} with email {Email}", user.UserName, user.Email ?? "N/A");
                    TempData["SuccessMessage"] = $"User '{user.UserName}' has been created successfully!";
                    return RedirectToPage("./Index");
                }
                else
                {
                    ModelState.AddModelError("", "Failed to create user. Please check the information and try again.");
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user {UserName}", UserInput.UserName);
                ModelState.AddModelError("", "An unexpected error occurred while creating the user.");
                return Page();
            }
        }
    }

    public class UserCreateModel
    {
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
        public bool SmsEnabled { get; set; } = true;

        [Display(Name = "Available on Special Days/Holidays")]
        public bool SpecialDaysEnabled { get; set; } = false;
    }
}