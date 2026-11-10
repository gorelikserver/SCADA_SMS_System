using System.Globalization;
using SCADASMSSystem.Web.Services;
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
