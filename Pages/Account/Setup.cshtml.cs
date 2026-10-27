using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Account
{
    /// <summary>
    /// First-run page: creates the first admin. Only reachable while no account exists, so there is
    /// no default password to forget to change.
    /// </summary>
    [AllowAnonymous]
    public class SetupModel : PageModel
    {
        private readonly IAccountService _accounts;
        private readonly ILogger<SetupModel> _logger;

        // Two first-run requests must not both create an admin.
        private static readonly SemaphoreSlim SetupLock = new(1, 1);

        public SetupModel(IAccountService accounts, ILogger<SetupModel> logger)
        {
            _accounts = accounts;
            _logger = logger;
        }

        [BindProperty, Required] public string UserName { get; set; } = "admin";
        [BindProperty, Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
        [BindProperty, Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
        public string? Error { get; private set; }

        public async Task<IActionResult> OnGetAsync() =>
            await _accounts.AnyAccountsAsync() ? RedirectToPage("/Account/Login") : Page();

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            await SetupLock.WaitAsync();
            try
            {
                if (await _accounts.AnyAccountsAsync())
                    return RedirectToPage("/Account/Login");

                var (ok, error) = await _accounts.CreateAsync(UserName, Password, AccountRoles.Admin);
                if (!ok)
                {
                    Error = error;
                    return Page();
                }
            }
            finally
            {
                SetupLock.Release();
            }

            _logger.LogWarning("First admin account {UserName} created from {RemoteIp}", UserName, HttpContext.Connection.RemoteIpAddress);
            var result = await _accounts.SignInAsync(UserName, Password);
            await SignInHelper.SignInAsync(HttpContext, result.Account!);
            return RedirectToPage("/Index");
        }
    }
}
