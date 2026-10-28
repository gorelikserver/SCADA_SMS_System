using System.Text.Json.Nodes;
using SCADASMSSystem.Web.Services;
using Xunit;

namespace SCADASMSSystem.Tests;

public sealed class SettingsLocationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "scada-loc-" + Guid.NewGuid().ToString("N"));

    public SettingsLocationTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Resolve_DefaultsToProgramDataOutsideInstallFolder()
    {
        var path = SettingsLocation.Resolve(null, @"C:\Program Files\SCADA SMS");
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        Assert.Equal(Path.Combine(programData, "SCADA_SMS", "config", "settings.json"), path);
    }

    [Fact]
    public void Resolve_HonoursAbsoluteAndRelativeOverrides()
    {
        Assert.Equal(Path.Combine(_dir, "x.json"), SettingsLocation.Resolve(Path.Combine(_dir, "x.json"), @"C:\ignored"));
        Assert.Equal(Path.GetFullPath(Path.Combine(_dir, "dev", "s.json")), SettingsLocation.Resolve(Path.Combine("dev", "s.json"), _dir));
    }

    [Fact]
    public void Seed_CopiesSmsSettingsFromInstallFile_IncludingProfile()
    {
        var install = Path.Combine(_dir, "appsettings.json");
        File.WriteAllText(install, """
            {
              // comments are allowed in appsettings.json
              "ConnectionStrings": { "DefaultConnection": "Server=.;Password=dbsecret" },
              "SmsSettings": { "RateLimit": 7, "Profile": { "Name": "Gateway", "Url": "https://gw" } },
            }
            """);
        var settings = Path.Combine(_dir, "config", "settings.json");

        Assert.True(SettingsLocation.EnsureSeeded(settings, install));

        var root = JsonNode.Parse(File.ReadAllText(settings))!;
        Assert.Equal(7, (int)root["SmsSettings"]!["RateLimit"]!);
        Assert.Equal("Gateway", (string?)root["SmsSettings"]!["Profile"]!["Name"]);
        Assert.Null(root["ConnectionStrings"]); // only SmsSettings moves
    }

    [Fact]
    public void Seed_NeverOverwritesExistingFile()
    {
        var install = Path.Combine(_dir, "appsettings.json");
        File.WriteAllText(install, """{ "SmsSettings": { "RateLimit": 1 } }""");
        var settings = Path.Combine(_dir, "settings.json");
        File.WriteAllText(settings, """{ "SmsSettings": { "RateLimit": 99 } }""");

        Assert.False(SettingsLocation.EnsureSeeded(settings, install));
        Assert.Contains("99", File.ReadAllText(settings));
    }

    [Fact]
    public void Seed_WithoutInstallFile_CreatesEmptySection()
    {
        var settings = Path.Combine(_dir, "new", "settings.json");
        Assert.True(SettingsLocation.EnsureSeeded(settings, Path.Combine(_dir, "missing.json")));
        Assert.NotNull(JsonNode.Parse(File.ReadAllText(settings))!["SmsSettings"]);
    }
}
