using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using SCADASMSSystem.Web.Services;
using Xunit;

namespace SCADASMSSystem.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "scada-settings-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public SettingsStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "appsettings.json");
        File.WriteAllText(_path, """
            {
              "ConnectionStrings": { "DefaultConnection": "Server=.;Database=x" },
              "SmsSettings": { "ApiEndpoint": "https://old", "Password": "s3cret" }
            }
            """);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private SettingsStore Store() => new(_path, configuration: null, NullLogger<SettingsStore>.Instance);

    private JsonObject ReadRoot() => JsonNode.Parse(File.ReadAllText(_path))!.AsObject();

    [Fact]
    public async Task Update_MergesSectionAndPreservesOtherSections()
    {
        await Store().UpdateSmsSettingsAsync(s => s["ApiEndpoint"] = "https://new");

        var root = ReadRoot();
        Assert.Equal("https://new", (string?)root["SmsSettings"]!["ApiEndpoint"]);
        Assert.Equal("s3cret", (string?)root["SmsSettings"]!["Password"]);
        Assert.Equal("Server=.;Database=x", (string?)root["ConnectionStrings"]!["DefaultConnection"]);
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public async Task Update_WritesBackupOfPreviousFile()
    {
        var original = File.ReadAllText(_path);
        var store = Store();

        await store.UpdateSmsSettingsAsync(s => s["ApiEndpoint"] = "https://new");

        var backup = Assert.Single(Directory.GetFiles(store.BackupDirectory));
        Assert.Equal(original, File.ReadAllText(backup));
    }

    [Fact]
    public async Task Update_KeepsAtMostTenBackups()
    {
        var store = Store();
        for (var i = 0; i < 13; i++)
        {
            await store.UpdateSmsSettingsAsync(s => s["RateLimit"] = i);
            await Task.Delay(2); // distinct millisecond timestamps in backup names
        }

        Assert.Equal(SettingsStore.BackupsToKeep, Directory.GetFiles(store.BackupDirectory).Length);
    }

    [Fact]
    public async Task Update_WhenMutateThrows_LeavesFileUntouched()
    {
        var original = File.ReadAllText(_path);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Store().UpdateSmsSettingsAsync(_ => throw new InvalidOperationException("boom")));

        Assert.Equal(original, File.ReadAllText(_path));
    }
}
