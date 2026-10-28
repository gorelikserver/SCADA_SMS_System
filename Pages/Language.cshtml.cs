using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SCADASMSSystem.Web.Pages;

public sealed class LanguageModel : PageModel
{
    public IActionResult OnGet(string culture, string? returnUrl = null)
    {
        var selectedCulture = culture.Equals("he-IL", StringComparison.OrdinalIgnoreCase) ? "he-IL" : "en";
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(selectedCulture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
