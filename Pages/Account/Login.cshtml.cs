using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly IAccountService _accounts;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(IAccountService accounts, ILogger<LoginModel> logger)
        {
            _accounts = accounts;
            _logger = logger;
        }

        [BindProperty, Required] public string UserName { get; set; } = string.Empty;
        [BindProperty, Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
        [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
        public string? Error { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // First run: nobody can log in until the first admin exists.
            if (!await _accounts.AnyAccountsAsync())
                return RedirectToPage("/Account/Setup");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var result = await _accounts.SignInAsync(UserName, Password);
            switch (result.Outcome)
            {
                case SignInOutcome.Success:
                    await SignInHelper.SignInAsync(HttpContext, result.Account!);
                    _logger.LogInformation("User {UserName} signed in", result.Account!.UserName);
                    return LocalRedirect(SignInHelper.SafeReturnUrl(ReturnUrl));
                case SignInOutcome.LockedOut:
                    _logger.LogWarning("Sign-in refused for locked account {UserName}", UserName);
                    Error = $"Too many failed attempts. Try again after {result.LockedUntil:HH:mm}.";
                    return Page();
                default:
                    _logger.LogWarning("Failed sign-in for {UserName}", UserName);
                    Error = "Wrong user name or password.";
                    return Page();
            }
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Account/Login");
        }
    }
}
