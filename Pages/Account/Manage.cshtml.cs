using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Account
{
    /// <summary>Admin-only (see Program.cs conventions): create, re-role, reset and delete accounts.</summary>
    public class ManageModel : PageModel
    {
        private readonly IAccountService _accounts;
        private readonly ILogger<ManageModel> _logger;

        public ManageModel(IAccountService accounts, ILogger<ManageModel> logger)
        {
            _accounts = accounts;
            _logger = logger;
        }

        public IReadOnlyList<Models.Account> Accounts { get; private set; } = [];

        public async Task OnGetAsync() => Accounts = await _accounts.ListAsync();

        public async Task<IActionResult> OnPostCreateAsync(string userName, string password, string role) =>
            await Report(_accounts.CreateAsync(userName, password, role), $"Account '{userName}' created.", "created account {0}", userName);

        public async Task<IActionResult> OnPostRoleAsync(int accountId, string role) =>
            await Report(_accounts.SetRoleAsync(accountId, role), "Role updated.", "set role of account {0} to " + role, accountId);

        public async Task<IActionResult> OnPostResetAsync(int accountId, string password) =>
            await Report(_accounts.ChangePasswordAsync(accountId, password), "Password reset.", "reset password of account {0}", accountId);

        public async Task<IActionResult> OnPostDeleteAsync(int accountId)
        {
            if (SignInHelper.CurrentAccountId(User) == accountId)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToPage();
            }
            return await Report(_accounts.DeleteAsync(accountId), "Account deleted.", "deleted account {0}", accountId);
        }

        private async Task<IActionResult> Report(Task<(bool Ok, string? Error)> action, string success, string auditFormat, object target)
        {
            var (ok, error) = await action;
            if (ok)
            {
                _logger.LogWarning("{Admin} {Action}", User.Identity?.Name, string.Format(auditFormat, target));
                TempData["SuccessMessage"] = success;
            }
            else
            {
                TempData["ErrorMessage"] = error;
            }
            return RedirectToPage();
        }
    }
}
