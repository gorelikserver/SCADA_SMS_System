using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Account
{
    /// <summary>Any signed-in user changes their own password.</summary>
    public class PasswordModel : PageModel
    {
        private readonly IAccountService _accounts;

        public PasswordModel(IAccountService accounts)
        {
            _accounts = accounts;
        }

        [BindProperty, Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = string.Empty;
        [BindProperty, Required, DataType(DataType.Password)] public string NewPassword { get; set; } = string.Empty;
        [BindProperty, Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
        public string? Error { get; private set; }
        public bool Done { get; private set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var check = await _accounts.SignInAsync(User.Identity?.Name ?? string.Empty, CurrentPassword);
            if (check.Outcome != SignInOutcome.Success)
            {
                Error = check.Outcome == SignInOutcome.LockedOut ? "Too many failed attempts; try again later." : "Current password is wrong.";
                return Page();
            }

            var (ok, error) = await _accounts.ChangePasswordAsync(check.Account!.AccountId, NewPassword);
            Error = error;
            Done = ok;
            return Page();
        }
    }
}
