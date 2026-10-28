using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Pages.Account
{
    internal static class SignInHelper
    {
        public const string AccountIdClaim = "account_id";

        public static Task SignInAsync(HttpContext context, Models.Account account)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, account.UserName),
                new Claim(ClaimTypes.Role, account.Role),
                new Claim(AccountIdClaim, account.AccountId.ToString()),
            }, CookieAuthenticationDefaults.AuthenticationScheme);

            return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }

        public static int? CurrentAccountId(ClaimsPrincipal user) =>
            int.TryParse(user.FindFirstValue(AccountIdClaim), out var id) ? id : null;

        /// <summary>Only local paths, so a crafted returnUrl cannot send the user to another site.</summary>
        public static string SafeReturnUrl(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\")
                ? returnUrl
                : "/";
    }
}
