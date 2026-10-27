using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Pages.Settings;
using SCADASMSSystem.Web.Services;
using Xunit;

namespace SCADASMSSystem.Tests;

/// <summary>
/// The Settings page edits only general fields. Saving must never touch the provider profile,
/// the draft, or anything else in the section (the old page once wiped credentials this way).
/// </summary>
public sealed class SettingsPageSaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "scada-page-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public SettingsPageSaveTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "appsettings.json");
        File.WriteAllText(_path, """
            {
              "SmsSettings": {
                "RateLimit": 10,
                "TimeoutSeconds": 30,
                "TestMode": false,
                "CustomKey": "keep-me",
                "Profile": {
                  "Name": "Gateway", "Url": "https://gw.example/send", "Body": "{\"to\":\"{Phone}\",\"t\":\"{Message}\"}",
                  "Variables": [ { "Key": "Token", "Value": "s3cret", "IsSecret": true } ]
                },
                "DraftProfile": { "Name": "Next", "Url": "https://gw2.example/send" }
              }
            }
            """);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private IndexModel CreatePage(SmsSettings posted, params string[] fields)
    {
        var store = new SettingsStore(_path, configuration: null, NullLogger<SettingsStore>.Instance);
        var page = new IndexModel(null!, null!, null!, null!, null!, null!, null!, store, NullLogger<IndexModel>.Instance);

        var form = fields.ToDictionary(f => "SmsSettingsInput." + f, _ => new StringValues("x"));
        var http = new DefaultHttpContext();
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Form = new FormCollection(form);

        page.PageContext = new PageContext { HttpContext = http };
        page.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        page.SmsSettingsInput = posted;
        return page;
    }

    private JsonNode Section() => JsonNode.Parse(File.ReadAllText(_path))!["SmsSettings"]!;

    [Fact]
    public async Task Save_ChangesOnlyPostedGeneralFields()
    {
        var page = CreatePage(new SmsSettings { RateLimit = 25, TimeoutSeconds = 20, TestMode = true },
            "RateLimit", "TimeoutSeconds", "TestMode", "Profile");

        await page.OnPostSaveSettingsAsync();

        var s = Section();
        Assert.Equal(25, (int)s["RateLimit"]!);
        Assert.Equal(20, (int)s["TimeoutSeconds"]!);
        Assert.True((bool)s["TestMode"]!);
        Assert.Equal("keep-me", (string?)s["CustomKey"]);
        Assert.Equal("s3cret", (string?)s["Profile"]!["Variables"]![0]!["Value"]);
        Assert.Equal("Next", (string?)s["DraftProfile"]!["Name"]);
    }

    [Fact]
    public async Task Save_WithOutOfRangeValue_LeavesFileUntouched()
    {
        var original = File.ReadAllText(_path);
        var page = CreatePage(new SmsSettings { TimeoutSeconds = 0 }, "TimeoutSeconds");

        await page.OnPostSaveSettingsAsync();

        Assert.Equal(original, File.ReadAllText(_path));
        Assert.StartsWith("Settings not saved", (string?)page.TempData["ErrorMessage"]);
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
