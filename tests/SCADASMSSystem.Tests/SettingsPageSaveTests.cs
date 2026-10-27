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
/// Regression for the credential wipe: saving the Settings page used to replace the whole
/// SmsSettings section, blanking every field the form does not render.
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
                "ProviderType": "REST",
                "ApiEndpoint": "https://gw.example/send",
                "Username": "gateway-user",
                "Password": "s3cret",
                "SenderName": "SCADA",
                "ApiParams": "{\"to\":\"phone\"}",
                "ApiHeaders": "{\"X-Site\":\"A\"}",
                "RestAuthType": "Bearer",
                "RestBearerToken": "tok-123",
                "RestApiKeyName": "X-API-Key",
                "RestApiKeyValue": "key-456",
                "SoapParams": "{\"Phone\":\"phone\"}",
                "RestHeaders": [ { "Key": "Authorization", "Value": "Bearer live", "IsSecret": true } ]
              }
            }
            """);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    // Exactly the field set the Settings form posts today (asp-for inputs + hidden JSON lists).
    private static readonly string[] FormFields =
    {
        "ApiEndpoint", "ContentType", "DuplicateWindow", "HttpMethod", "LogSoapXml", "ProviderType",
        "RateLimit", "RateWindow", "RestAuthType", "RestFailurePattern", "RestSuccessPattern",
        "RetryAttempts", "ScadaPcimObjectId", "SoapAction", "SoapAuthType", "SoapBodyTemplate",
        "SoapEnvelopeNamespaces", "SoapErrorPattern", "SoapMessageType", "SoapSendingSystem",
        "SoapSuccessPattern", "TestMode", "TimeoutSeconds",
    };

    private IndexModel CreatePage(SmsSettings posted, string restHeadersJson)
    {
        var store = new SettingsStore(_path, configuration: null, NullLogger<SettingsStore>.Instance);
        var page = new IndexModel(null!, null!, null!, null!, null!, null!, null!, store, NullLogger<IndexModel>.Instance);

        var form = FormFields.ToDictionary(f => "SmsSettingsInput." + f, _ => new StringValues("x"));
        form["RestHeadersJson"] = restHeadersJson;
        form["RestBodyParamsJson"] = "[]";
        form["SoapPlaceholdersJson"] = "[]";

        var http = new DefaultHttpContext();
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Form = new FormCollection(form);

        page.PageContext = new PageContext { HttpContext = http };
        page.TempData = new TempDataDictionary(http, new NullTempDataProvider());
        page.SmsSettingsInput = posted;
        page.RestHeadersJson = restHeadersJson;
        page.RestBodyParamsJson = "[]";
        page.SoapPlaceholdersJson = "[]";
        return page;
    }

    [Fact]
    public async Task Save_KeepsCredentialsAndFieldsTheFormDoesNotPost()
    {
        var posted = new SmsSettings { ApiEndpoint = "https://gw.example/v2/send", RestAuthType = "Bearer", RateLimit = 20 };
        var page = CreatePage(posted, """[ { "Key": "Authorization", "Value": "***", "IsSecret": true } ]""");

        await page.OnPostSaveSettingsAsync();

        var s = JsonNode.Parse(File.ReadAllText(_path))!["SmsSettings"]!;
        Assert.Equal("https://gw.example/v2/send", (string?)s["ApiEndpoint"]);
        Assert.Equal(20, (int)s["RateLimit"]!);
        Assert.Equal("gateway-user", (string?)s["Username"]);
        Assert.Equal("s3cret", (string?)s["Password"]);
        Assert.Equal("SCADA", (string?)s["SenderName"]);
        Assert.Equal("{\"to\":\"phone\"}", (string?)s["ApiParams"]);
        Assert.Equal("{\"X-Site\":\"A\"}", (string?)s["ApiHeaders"]);
        Assert.Equal("tok-123", (string?)s["RestBearerToken"]);
        Assert.Equal("X-API-Key", (string?)s["RestApiKeyName"]);
        Assert.Equal("key-456", (string?)s["RestApiKeyValue"]);
        Assert.Equal("{\"Phone\":\"phone\"}", (string?)s["SoapParams"]);
        Assert.Equal("Bearer live", (string?)s["RestHeaders"]![0]!["Value"]);
        Assert.Equal("Settings saved successfully. Changes are active immediately.", page.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task Save_WithCorruptParamList_RejectsAndLeavesFileUntouched()
    {
        var original = File.ReadAllText(_path);
        var page = CreatePage(new SmsSettings(), "{broken");

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
