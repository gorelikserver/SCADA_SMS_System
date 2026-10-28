using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using SCADASMSSystem.Web.Services.Providers;
using Xunit;

namespace SCADASMSSystem.Tests;

/// <summary>In-memory ISettingsHistory: the EF implementation only adds persistence and error swallowing.</summary>
public sealed class FakeSettingsHistory : ISettingsHistory
{
    public List<SettingsChange> Changes { get; } = new();
    public List<ProviderProfileVersion> Versions { get; } = new();

    public Task RecordChangeAsync(string area, string action, string summary, string? user)
    {
        Changes.Add(new SettingsChange { Id = Changes.Count + 1, Area = area, Action = action, Summary = summary, ChangedBy = user });
        return Task.CompletedTask;
    }

    public Task RecordVersionAsync(ProviderProfileVersion version)
    {
        version.Id = Versions.Count + 1;
        Versions.Add(version);
        return Task.CompletedTask;
    }

    public Task<bool> HasVersionAsync(string profileHash) => Task.FromResult(Versions.Any(v => v.ProfileHash == profileHash));

    public Task<IReadOnlyList<SettingsChange>> GetChangesAsync(int take) =>
        Task.FromResult<IReadOnlyList<SettingsChange>>(Changes.AsEnumerable().Reverse().Take(take).ToList());

    public Task<IReadOnlyList<ProviderProfileVersion>> GetVersionsAsync(int take) =>
        Task.FromResult<IReadOnlyList<ProviderProfileVersion>>(Versions.AsEnumerable().Reverse().Take(take).ToList());

    public Task<ProviderProfileVersion?> GetVersionAsync(int id) => Task.FromResult(Versions.FirstOrDefault(v => v.Id == id));
}

public class SettingsChangeSummaryTests
{
    private static readonly string[] Fields = { "RateLimit", "TimeoutSeconds", "TestMode", "LogSoapXml" };

    [Fact]
    public void DiffGeneral_ListsChangedFieldsWithOldAndNewValues()
    {
        var before = new SmsSettings { TimeoutSeconds = 30, TestMode = false, RateLimit = 10 };
        var after = new SmsSettings { TimeoutSeconds = 20, TestMode = true, RateLimit = 10 };
        Assert.Equal("TimeoutSeconds 30 → 20, TestMode false → true", SettingsChangeSummary.DiffGeneral(before, after, Fields));
    }

    [Fact]
    public void DiffGeneral_NoChange_IsNull()
    {
        Assert.Null(SettingsChangeSummary.DiffGeneral(new SmsSettings(), new SmsSettings(), Fields));
    }

    [Fact]
    public void DiffProfile_NeverShowsSecretValues()
    {
        var before = new ProviderProfile
        {
            Name = "Gw", Method = "POST", Url = "https://gw/send?key=raw-key-1",
            Variables =
            {
                new SmsParam { Key = "Password", Value = "old-pass", IsSecret = true },
                new SmsParam { Key = "Sender", Value = "SCADA" },
                new SmsParam { Key = "Flip", Value = "was-plain" },
            },
        };
        var after = ProfileManagement.Clone(before);
        after.Method = "GET";
        after.Url = "https://gw/send?key=raw-key-2";
        after.Variables[0].Value = "new-pass";
        after.Variables[1].Value = "PLANT";
        after.Variables[2].Value = "now-secret";
        after.Variables[2].IsSecret = true;
        after.Variables.Add(new SmsParam { Key = "Token", Value = "tok-123", IsSecret = true });

        var summary = SettingsChangeSummary.DiffProfile(before, after)!;

        Assert.Contains("Method POST → GET", summary);
        Assert.Contains("URL changed", summary);
        Assert.Contains("secret Password changed", summary);
        Assert.Contains("variable Sender SCADA → PLANT", summary);
        Assert.Contains("secret Flip changed", summary);
        Assert.Contains("secret Token added", summary);
        foreach (var secret in new[] { "old-pass", "new-pass", "now-secret", "was-plain", "tok-123", "raw-key" })
            Assert.DoesNotContain(secret, summary);
    }

    [Fact]
    public void DiffProfile_SameProfile_IsNull()
    {
        var p = new ProviderProfile { Name = "Gw", Variables = { new SmsParam { Key = "K", Value = "v", IsSecret = true } } };
        Assert.Null(SettingsChangeSummary.DiffProfile(p, ProfileManagement.Clone(p)));
    }
}

public sealed class ProfileHistoryFlowTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "scada-history-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;
    private readonly SettingsStore _store;
    private readonly FakeSettingsHistory _history = new();
    private readonly ProviderProfileService _service;

    public ProfileHistoryFlowTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "appsettings.json");
        File.WriteAllText(_path, """{ "SmsSettings": { "TestMode": true } }""");
        _store = new SettingsStore(_path, configuration: null, NullLogger<SettingsStore>.Instance);
        _service = new ProviderProfileService(_store, new ProviderProfileServiceTests.FakeSms(), _history, NullLogger<ProviderProfileService>.Instance);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static ProviderProfile Valid(string token, string name = "Gateway") => new()
    {
        Name = name,
        Url = "https://gw.example/send",
        Body = """{"to":"{Phone}","text":"{Message}"}""",
        Variables = { new SmsParam { Key = "Token", Value = token, IsSecret = true } },
        Auth = new ProfileAuth { Type = AuthTypes.Bearer, Secret = "{Token}" },
    };

    private async Task ActivateAsync(ProviderProfile profile)
    {
        await _service.SaveDraftAsync(profile);
        Assert.True((await _service.VerifyDraftAsync("050-1234567", "hi", "admin")).Accepted);
        Assert.True((await _service.ActivateDraftAsync("admin")).Ok);
    }

    private string AllSummaries() => string.Join("\n", _history.Changes.Select(c => c.Summary));

    [Fact]
    public async Task Activation_RecordsVersionWithFullProfileAndHash()
    {
        await ActivateAsync(Valid("secret-1"));

        var version = Assert.Single(_history.Versions);
        var active = (await _store.ReadSmsSettingsAsync()).Profile!;
        Assert.Equal(ProfileManagement.Hash(active), version.ProfileHash);
        Assert.Equal("Gateway", version.ProfileName);
        Assert.Equal("REST", version.ProfileKind);
        Assert.Equal("admin", version.ActivatedBy);
        Assert.Equal("admin", version.VerifiedBy);
        Assert.NotNull(version.VerifiedAt);
        Assert.Null(version.Note);
        // The version is the rollback source, so it keeps the secret.
        Assert.Equal("secret-1", JsonSerializer.Deserialize<ProviderProfile>(version.ProfileJson)!.Variables[0].Value);

        Assert.Contains(_history.Changes, c => c.Action == SettingsActions.ProfileActivated);
        Assert.Contains(_history.Changes, c => c.Action == SettingsActions.DraftVerified);
    }

    [Fact]
    public async Task Summaries_NeverContainSecrets()
    {
        await ActivateAsync(Valid("secret-one"));
        await ActivateAsync(Valid("secret-two"));
        await _service.RestoreVersionAsDraftAsync(_history.Versions[0].Id, "admin");
        await _service.DiscardDraftAsync("admin");

        var all = AllSummaries();
        Assert.Contains("secret Token changed", all);
        Assert.DoesNotContain("secret-one", all);
        Assert.DoesNotContain("secret-two", all);
    }

    [Fact]
    public async Task PreexistingActiveProfile_IsCapturedOnceBeforeItIsReplaced()
    {
        // Converted at startup: active profile, no activation record, no version.
        await _store.UpdateSmsSettingsAsync(section => ProfileManagement.WriteProfile(section, "Profile", Valid("legacy", "Converted")));

        await _service.EnsureActiveVersionRecordedAsync();
        await _service.EnsureActiveVersionRecordedAsync();
        var captured = Assert.Single(_history.Versions);
        Assert.Equal("Converted", captured.ProfileName);
        Assert.NotNull(captured.Note);

        await ActivateAsync(Valid("new"));
        Assert.Equal(2, _history.Versions.Count);
        Assert.Equal("Gateway", _history.Versions[1].ProfileName);
    }

    [Fact]
    public async Task PreexistingActiveProfile_WithoutHistoryView_IsCapturedAtActivation()
    {
        await _store.UpdateSmsSettingsAsync(section => ProfileManagement.WriteProfile(section, "Profile", Valid("legacy", "Converted")));

        await ActivateAsync(Valid("new"));

        Assert.Equal(new[] { "Converted", "Gateway" }, _history.Versions.Select(v => v.ProfileName));
        Assert.Equal("legacy", JsonSerializer.Deserialize<ProviderProfile>(_history.Versions[0].ProfileJson)!.Variables[0].Value);
    }

    [Fact]
    public async Task RestoreAsDraft_PutsVersionIntoDraft_AndLeavesActiveProfileAlone()
    {
        await ActivateAsync(Valid("first", "First"));
        await ActivateAsync(Valid("second", "Second"));
        var activeBefore = (await _store.ReadSmsSettingsAsync()).Profile!;
        var first = _history.Versions.Single(v => v.ProfileName == "First");

        var (ok, _) = await _service.RestoreVersionAsDraftAsync(first.Id, "admin");

        Assert.True(ok);
        var s = await _store.ReadSmsSettingsAsync();
        Assert.Equal(first.ProfileHash, ProfileManagement.Hash(s.DraftProfile!));
        Assert.Equal("first", s.DraftProfile!.Variables[0].Value);
        Assert.Equal(ProfileManagement.Hash(activeBefore), ProfileManagement.Hash(s.Profile!));
        Assert.Equal("Second", s.Profile!.Name);
        var change = _history.Changes.Last();
        Assert.Equal(SettingsActions.ProfileRestoredAsDraft, change.Action);
        Assert.Equal("admin", change.ChangedBy);
    }

    [Fact]
    public async Task RestoredDraft_StillNeedsVerificationBeforeActivation()
    {
        // The first profile was verified once already; restoring it must not reuse that verification.
        await ActivateAsync(Valid("first", "First"));
        var first = _history.Versions[0];
        await _service.RestoreVersionAsDraftAsync(first.Id, "admin");

        var (ok, message) = await _service.ActivateDraftAsync("admin");
        Assert.False(ok);
        Assert.Contains("Verify", message);
        Assert.False((await _service.GetStateAsync()).DraftVerified);

        Assert.True((await _service.VerifyDraftAsync("050-1234567", "hi", "admin")).Accepted);
        Assert.True((await _service.ActivateDraftAsync("admin")).Ok);
    }

    [Fact]
    public async Task RestoreUnknownVersion_ChangesNothing()
    {
        await ActivateAsync(Valid("first"));
        var before = File.ReadAllText(_path);

        var (ok, _) = await _service.RestoreVersionAsDraftAsync(999, "admin");

        Assert.False(ok);
        Assert.Equal(before, File.ReadAllText(_path));
    }

    [Fact]
    public async Task DiscardWithoutDraft_RecordsNothing()
    {
        await _service.DiscardDraftAsync("admin");
        Assert.Empty(_history.Changes);

        await _service.SaveDraftAsync(Valid("x"));
        await _service.DiscardDraftAsync("admin");
        Assert.Equal(SettingsActions.DraftDiscarded, Assert.Single(_history.Changes).Action);
    }
}

public class SettingsHistoryDatabaseFailureTests
{
    [Fact]
    public async Task RecordingNeverThrows_WhenTheDatabaseIsUnreachable()
    {
        var options = new DbContextOptionsBuilder<SCADADbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=none;User Id=x;Password=x;Connect Timeout=1;TrustServerCertificate=True")
            .Options;
        using var db = new SCADADbContext(options);
        var history = new SettingsHistory(db, NullLogger<SettingsHistory>.Instance);

        await history.RecordChangeAsync(SettingsAreas.General, SettingsActions.GeneralSaved, "TestMode false → true", "admin");
        await history.RecordVersionAsync(new ProviderProfileVersion { ProfileHash = "AB", ProfileJson = "{}" });
        Assert.True(await history.HasVersionAsync("AB"));
    }
}
