using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Pages;
using SCADASMSSystem.Web.Pages.Groups;
using SCADASMSSystem.Web.Pages.Users;
using SCADASMSSystem.Web.Services;
using SettingsIndexModel = SCADASMSSystem.Web.Pages.Settings.IndexModel;
using SmsSettings = SCADASMSSystem.Web.Models.SmsSettings;
using Xunit;

namespace SCADASMSSystem.Tests;

public sealed class UiLocalizationTests
{
    [Fact]
    public void Hebrew_page_titles_have_server_side_translations()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("he-IL");
            CultureInfo.CurrentUICulture = new CultureInfo("he-IL");
            var localizer = new UiLocalizer();
            var titles = new[]
            {
                "Operations overview", "Delivery Alerts", "Delivery History", "Recipients",
                "Notification Teams", "Routing Rules", "System Settings", "SMS Provider",
                "Settings History", "Test SMS", "Coverage Calendar", "Integration Guide",
                "About AlarmBridge", "Edit Recipient", "Add Recipient",
                "Edit Notification Team", "Create Notification Team", "Error"
            };

            Assert.All(titles, title => Assert.NotEqual(title, localizer[title]));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void English_localizer_preserves_source_text()
    {
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var localizer = new UiLocalizer();
            Assert.Equal("Operations overview", localizer["Operations overview"]);
            Assert.Equal("Primary navigation", localizer["Primary navigation"]);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void Hebrew_client_dictionary_covers_dynamic_and_error_states()
    {
        var script = File.ReadAllText(FindRepositoryFile("wwwroot", "js", "hebrew-ui.js"));
        var requiredDynamicText = new[]
        {
            "Error loading recent activity", "No recent activity", "Wrong user name or password.",
            "Settings saved.", "Recipient created.", "Team created.",
            "No delivery attempts match the current filters. Try broadening the search or date range.",
            "AlarmBridge encountered an unexpected problem. Try again, or give the request ID below to your administrator."
        };

        Assert.All(requiredDynamicText, text => Assert.Contains($"'{text}'", script, StringComparison.Ordinal));
        Assert.Contains("characterData:true", script, StringComparison.Ordinal);
        Assert.Contains("attributes:true", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("he-IL", "he-IL")]
    [InlineData("HE-il", "he-IL")]
    [InlineData("unsupported", "en")]
    public void Language_route_normalizes_missing_and_supported_cultures(string? requested, string expected) =>
        Assert.Equal(expected, LanguageModel.NormalizeCulture(requested));

    [Fact]
    public void Hebrew_client_dictionary_has_no_duplicate_keys()
    {
        var script = File.ReadAllText(FindRepositoryFile("wwwroot", "js", "hebrew-ui.js"));
        var body = Regex.Match(script, @"const he = \{(?<body>[\s\S]*?)\r?\n  \};").Groups["body"].Value;
        var keys = Regex.Matches(body, @"(?:^|,)\s*'(?<key>(?:\\.|[^'\\])+)'\s*:")
            .Select(match => match.Groups["key"].Value)
            .ToList();
        Assert.True(keys.Count > 400, $"Expected to parse the whole dictionary, parsed {keys.Count} keys.");
        var duplicates = keys.GroupBy(key => key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        Assert.True(duplicates.Count == 0, "Duplicate keys in hebrew-ui.js: " + string.Join(" | ", duplicates));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr-FR")]
    public void Language_page_with_missing_culture_redirects_to_english(string? culture)
    {
        var (result, cookie) = InvokeLanguage(culture, "/Users?page=2");
        Assert.Equal("/Users?page=2", Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.Contains("c%3Den%7Cuic%3Den", cookie, StringComparison.Ordinal);
    }

    [Fact]
    public void Language_page_switches_only_the_ui_culture_to_hebrew()
    {
        var (result, cookie) = InvokeLanguage("he-IL", "https://evil.example/");
        Assert.Equal("/", Assert.IsType<LocalRedirectResult>(result).Url);
        Assert.Contains("c%3Den%7Cuic%3Dhe-IL", cookie, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("c=en|uic=he-IL")]
    [InlineData("c=he-IL|uic=he-IL")] // cookie written by an earlier build
    public async Task Hebrew_request_keeps_english_formatting_culture(string cookieValue)
    {
        var options = new RequestLocalizationOptions();
        UiLocalizer.ConfigureRequestLocalization(options);
        CultureInfo? culture = null, uiCulture = null;
        string? formattedDate = null, formattedNumber = null;
        DateTime parsedDate = default;
        var middleware = new RequestLocalizationMiddleware(
            _ =>
            {
                culture = CultureInfo.CurrentCulture;
                uiCulture = CultureInfo.CurrentUICulture;
                // What CSV export, lockout times and posted-value parsing see during a Hebrew request.
                formattedDate = new DateTime(2026, 3, 4, 14, 5, 0).ToString();
                formattedNumber = 1234.5.ToString("N1");
                parsedDate = DateTime.Parse("03/04/2026");
                return Task.CompletedTask;
            },
            Options.Create(options), NullLoggerFactory.Instance);
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{CookieRequestCultureProvider.DefaultCookieName}={Uri.EscapeDataString(cookieValue)}";

        await middleware.Invoke(context);

        Assert.Equal("he-IL", uiCulture?.Name);
        Assert.Equal("en", culture?.Name);
        Assert.Equal("3/4/2026 2:05:00 PM", formattedDate);
        Assert.Equal("1,234.5", formattedNumber);
        Assert.Equal(new DateTime(2026, 3, 4), parsedDate);
    }

    [Fact]
    public void Account_messages_are_localized_without_touching_values()
    {
        Assert.Equal("Account not found.", WithUiCulture("en", () => UiLocalizer.LocalizeAccountMessage("Account not found.")));
        Assert.Equal("החשבון לא נמצא.", WithUiCulture("he-IL", () => UiLocalizer.LocalizeAccountMessage("Account not found.")));
        Assert.Equal("הסיסמה חייבת להכיל לפחות 8 תווים.",
            WithUiCulture("he-IL", () => UiLocalizer.LocalizeAccountMessage("Password must be at least 8 characters.")));
        Assert.Equal("החשבון 'ops' נוצר.", WithUiCulture("he-IL", () => UiLocalizer.Format("Account '{0}' created.", "ops")));
        Assert.Null(WithUiCulture("he-IL", () => UiLocalizer.LocalizeAccountMessage(null)));
    }

    [Fact]
    public void Model_binding_messages_follow_the_ui_culture()
    {
        var options = new MvcOptions();
        UiLocalizer.ConfigureModelBindingMessages(options);
        var messages = options.ModelBindingMessageProvider;

        Assert.Equal("The value '' is invalid.", WithUiCulture("en", () => messages.ValueMustNotBeNullAccessor("")));
        Assert.Equal("The value 'abc' is not valid for Target Group.",
            WithUiCulture("en", () => messages.AttemptedValueIsInvalidAccessor("abc", "Target Group")));
        Assert.Equal("הערך '' אינו תקין.", WithUiCulture("he-IL", () => messages.ValueMustNotBeNullAccessor("")));
        Assert.Equal("הערך 'abc' אינו תקין עבור צוות יעד.",
            WithUiCulture("he-IL", () => messages.AttemptedValueIsInvalidAccessor("abc", "Target Group")));
        Assert.Equal("השדה צוות יעד חייב להיות מספר.",
            WithUiCulture("he-IL", () => messages.ValueMustBeANumberAccessor("Target Group")));
    }

    [Fact]
    public void Validation_messages_follow_the_ui_culture()
    {
        var model = new UserCreateModel { UserName = "", PhoneNumber = "abc", Email = "not-an-email" };

        var english = WithUiCulture("en", () => Validate(model));
        Assert.Contains("The Full Name field is required.", english);
        Assert.Contains("The Email Address (Optional) field is not a valid e-mail address.", english);
        Assert.Contains("The Phone Number field is not a valid phone number.", english);

        var hebrew = WithUiCulture("he-IL", () => Validate(model));
        Assert.Contains("השדה שם מלא הוא שדה חובה.", hebrew);
        Assert.Contains("כתובת הדוא״ל אינה תקינה.", hebrew);
        Assert.Contains("מספר הטלפון אינו תקין.", hebrew);
        Assert.DoesNotContain(hebrew, message => message.StartsWith("The ", StringComparison.Ordinal));

        var shortName = new GroupCreateModel { GroupName = "x" };
        Assert.Contains("אורך השדה שם צוות חייב להיות בין 2 ל־255 תווים.", WithUiCulture("he-IL", () => Validate(shortName)));
        Assert.Contains("The field Group Name must be a string with a minimum length of 2 and a maximum length of 255.",
            WithUiCulture("en", () => Validate(shortName)));
    }

    [Fact]
    public void Server_messages_are_formatted_in_the_ui_language()
    {
        Assert.Equal("Group 'Ops' has been deleted successfully!",
            WithUiCulture("en", () => UiLocalizer.Format("Group '{0}' has been deleted successfully!", "Ops")));
        Assert.Equal("הצוות 'Ops' נמחק בהצלחה!",
            WithUiCulture("he-IL", () => UiLocalizer.Format("Group '{0}' has been deleted successfully!", "Ops")));
    }

    [Fact]
    public void Settings_validation_errors_stay_unchanged_in_english()
    {
        var invalid = new SmsSettings
        {
            RateLimit = 0, RateWindow = 0, DuplicateWindow = -1,
            TimeoutSeconds = 1, RetryAttempts = 11, ScadaPcimObjectId = 0
        };

        Assert.Equal(
            "Settings not saved: Rate limit must be 1-10000. Rate window must be 1-86400 seconds. " +
            "Duplicate window must be 0-1440 minutes. Timeout must be 3-300 seconds. " +
            "Retry attempts must be 0-10. PCIM Object ID must be a positive number.",
            WithUiCulture("en", () => SettingsIndexModel.BuildGeneralSettingsError(invalid)));
        Assert.Null(WithUiCulture("en", () => SettingsIndexModel.BuildGeneralSettingsError(new SmsSettings
        {
            RateLimit = 10, RateWindow = 60, DuplicateWindow = 5,
            TimeoutSeconds = 30, RetryAttempts = 3, ScadaPcimObjectId = 1
        })));
    }

    [Fact]
    public void Settings_validation_errors_are_localized_in_hebrew()
    {
        var invalid = new SmsSettings
        {
            RateLimit = 0, RateWindow = 0, DuplicateWindow = -1,
            TimeoutSeconds = 1, RetryAttempts = 11, ScadaPcimObjectId = 0
        };

        Assert.Equal(
            "ההגדרות לא נשמרו: מגבלת הקצב חייבת להיות בין 1 ל־10000. חלון הקצב חייב להיות בין 1 ל־86400 שניות. " +
            "חלון הכפילויות חייב להיות בין 0 ל־1440 דקות. זמן הקצוב חייב להיות בין 3 ל־300 שניות. " +
            "מספר הניסיונות החוזרים חייב להיות בין 0 ל־10. מזהה אובייקט PCIM חייב להיות מספר חיובי.",
            WithUiCulture("he-IL", () => SettingsIndexModel.BuildGeneralSettingsError(invalid)));
    }

    [Fact]
    public void Hebrew_server_translations_keep_every_format_placeholder()
    {
        var field = typeof(UiLocalizer).GetField("Hebrew", BindingFlags.NonPublic | BindingFlags.Static)!;
        var dictionary = (IReadOnlyDictionary<string, string>)field.GetValue(null)!;
        static string[] Placeholders(string text) =>
            Regex.Matches(text, @"\{\d+\}").Select(match => match.Value).Distinct().OrderBy(value => value).ToArray();

        // Sealed DataAnnotations attributes format with the English display name, so these Hebrew
        // messages name the field themselves instead of using {0}.
        var mayOmitFieldName = new HashSet<string>
        {
            "The {0} field is not a valid phone number.",
            "The {0} field is not a valid e-mail address."
        };

        Assert.All(dictionary, entry =>
        {
            var expected = Placeholders(entry.Key);
            var actual = Placeholders(entry.Value);
            Assert.Subset(expected.ToHashSet(), actual.ToHashSet());
            if (!mayOmitFieldName.Contains(entry.Key))
                Assert.Equal(expected, actual);
        });
    }

    [Fact]
    public void Provider_validation_errors_are_localized_without_touching_values()
    {
        Assert.Equal("URL is required.", WithUiCulture("en", () => UiLocalizer.LocalizeProviderError("URL is required.")));
        WithUiCulture("he-IL", () =>
        {
            Assert.Equal("חובה להזין כתובת URL.", UiLocalizer.LocalizeProviderError("URL is required."));
            Assert.Equal("המשתנה 'Token' מוגדר פעמיים.", UiLocalizer.LocalizeProviderError("Variable 'Token' is defined twice."));
            Assert.Equal("כלל הצלחה: חובה להזין ביטוי עבור Regex.", UiLocalizer.LocalizeProviderError("Success rule: expression is required for Regex."));
            Assert.Equal("Unknown provider text", UiLocalizer.LocalizeProviderError("Unknown provider text"));
            return 0;
        });
    }

    private static (IActionResult Result, string Cookie) InvokeLanguage(string? culture, string returnUrl)
    {
        var context = new DefaultHttpContext();
        var page = new LanguageModel
        {
            PageContext = new PageContext { HttpContext = context },
            Url = new LocalOnlyUrlHelper()
        };
        var result = page.OnGet(culture, returnUrl);
        return (result, context.Response.Headers.SetCookie.ToString());
    }

    private static List<string> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results.Select(result => result.ErrorMessage ?? string.Empty).ToList();
    }

    private static T WithUiCulture<T>(string name, Func<T> action)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en");
            CultureInfo.CurrentUICulture = new CultureInfo(name);
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private sealed class LocalOnlyUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public bool IsLocalUrl(string? url) =>
            !string.IsNullOrEmpty(url) && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
        public string? Action(UrlActionContext actionContext) => throw new NotSupportedException();
        public string? Content(string? contentPath) => throw new NotSupportedException();
        public string? Link(string? routeName, object? values) => throw new NotSupportedException();
        public string? RouteUrl(UrlRouteContext routeContext) => throw new NotSupportedException();
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file {Path.Combine(parts)}");
    }
}
