using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using SCADASMSSystem.Web.Services.Providers;
using Xunit;

namespace SCADASMSSystem.Tests;

public sealed class ProviderProfileServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "scada-profile-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;
    private readonly SettingsStore _store;
    private readonly FakeSms _sms = new();
    private readonly ProviderProfileService _service;

    public ProviderProfileServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "appsettings.json");
        File.WriteAllText(_path, """{ "SmsSettings": { "TestMode": true } }""");
        _store = new SettingsStore(_path, configuration: null, NullLogger<SettingsStore>.Instance);
        _service = new ProviderProfileService(_store, _sms, NullLogger<ProviderProfileService>.Instance);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static ProviderProfile Valid(string token = "t-1") => new()
    {
        Name = "Gateway",
        Url = "https://gw.example/send",
        Body = """{"to":"{Phone}","text":"{Message}"}""",
        Variables = { new SmsParam { Key = "Token", Value = token, IsSecret = true } },
        Auth = new ProfileAuth { Type = AuthTypes.Bearer, Secret = "{Token}" },
    };

    private static ProviderProfile Masked(ProviderProfile p) => ProfileManagement.MaskSecrets(p);

    [Fact]
    public async Task FullFlow_SaveVerifyActivate()
    {
        Assert.Empty(await _service.SaveDraftAsync(Valid()));
        var outcome = await _service.VerifyDraftAsync("050-1234567", "hi", "admin");
        Assert.True(outcome.Accepted);
        Assert.Equal("t-1", _sms.LastProfile!.Variables[0].Value);

        var (ok, _) = await _service.ActivateDraftAsync("admin");
        Assert.True(ok);
        var s = await _store.ReadSmsSettingsAsync();
        Assert.Equal("Gateway", s.Profile!.Name);
        Assert.Null(s.DraftProfile);
        Assert.Equal("admin", s.Activation!.ActivatedBy);
        Assert.True(s.TestMode); // untouched
    }

    [Fact]
    public async Task Activate_WithoutVerification_IsRefused()
    {
        await _service.SaveDraftAsync(Valid());
        var (ok, message) = await _service.ActivateDraftAsync("admin");
        Assert.False(ok);
        Assert.Contains("Verify", message);
        Assert.Null((await _store.ReadSmsSettingsAsync()).Profile);
    }

    [Fact]
    public async Task EditAfterVerification_InvalidatesIt()
    {
        await _service.SaveDraftAsync(Valid());
        await _service.VerifyDraftAsync("050-1234567", "hi", "admin");

        var changed = Masked(Valid());
        changed.Url = "https://gw.example/v2/send";
        await _service.SaveDraftAsync(changed);

        Assert.False((await _service.GetStateAsync()).DraftVerified);
        Assert.False((await _service.ActivateDraftAsync("admin")).Ok);
    }

    [Fact]
    public async Task RejectedVerification_IsNotRecorded()
    {
        _sms.Accept = false;
        await _service.SaveDraftAsync(Valid());
        var outcome = await _service.VerifyDraftAsync("050-1234567", "hi", "admin");
        Assert.False(outcome.Accepted);
        Assert.False((await _service.GetStateAsync()).DraftVerified);
    }

    [Fact]
    public async Task InvalidDraft_IsSavedButNotSent()
    {
        var broken = Valid();
        broken.Body = """{"text":"{Message}"}""";
        var errors = await _service.SaveDraftAsync(broken);
        Assert.NotEmpty(errors);
        Assert.NotNull((await _store.ReadSmsSettingsAsync()).DraftProfile);

        var outcome = await _service.VerifyDraftAsync("050-1234567", "hi", "admin");
        Assert.False(outcome.Accepted);
        Assert.Null(_sms.LastProfile);
    }

    [Fact]
    public async Task MaskedSecret_KeepsStoredValue_FromDraftThenActive()
    {
        await _service.SaveDraftAsync(Valid("from-draft"));
        await _service.SaveDraftAsync(Masked(Valid("ignored")));
        Assert.Equal("from-draft", (await _store.ReadSmsSettingsAsync()).DraftProfile!.Variables[0].Value);

        // No draft: the active profile supplies the secret.
        await _service.VerifyDraftAsync("050-1234567", "hi", "admin");
        await _service.ActivateDraftAsync("admin");
        await _service.SaveDraftAsync(Masked(Valid("ignored")));
        Assert.Equal("from-draft", (await _store.ReadSmsSettingsAsync()).DraftProfile!.Variables[0].Value);
    }

    [Fact]
    public async Task Verify_RejectsInvalidPhone()
    {
        await _service.SaveDraftAsync(Valid());
        Assert.False((await _service.VerifyDraftAsync("abc", "hi", "admin")).Accepted);
        Assert.Null(_sms.LastProfile);
    }

    private sealed class FakeSms : ISmsService
    {
        public bool Accept { get; set; } = true;
        public ProviderProfile? LastProfile { get; private set; }

        public Task<ProfileSendResult> SendWithProfileAsync(ProviderProfile profile, ProfileContext context, CancellationToken cancellationToken = default)
        {
            LastProfile = profile;
            return Task.FromResult(new ProfileSendResult(
                new SmsApiResponse { Success = Accept, StatusCode = Accept ? 200 : 400, Message = Accept ? "ok" : "rejected" }, "url", "body"));
        }

        public Task<bool> SendSmsAsync(string message, IEnumerable<SmsRecipient> recipients, string alarmId, int? groupId = null, string? priority = null, string? value = null) => throw new NotSupportedException();
        public Task<bool> SendSmsToGroupAsync(string message, int groupId, string alarmId, string? priority = null, string? value = null) => throw new NotSupportedException();
        public Task<CriticalSmsDeliveryResult> SendCriticalSmsToGroupAsync(string message, int groupId, string alarmId, string? priority = null, string? value = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SmsApiResponse> SendSmsApiCallAsync(string message, SmsRecipient recipient) => throw new NotSupportedException();
    }
}

public class ProfileManagementTests
{
    [Fact]
    public void Cutover_ConvertsLegacyFieldsAndRemovesThem()
    {
        var section = JsonNode.Parse("""
            {
              "ProviderType": "REST", "ApiEndpoint": "https://gw.example/send", "HttpMethod": "GET",
              "ApiParams": "{\"to\":\"phone\",\"msg\":\"message\"}", "Password": "pw",
              "RateLimit": 7, "TestMode": true, "ProfileSource": "Legacy"
            }
            """)!.AsObject();

        var profile = ProfileManagement.CutoverLegacy(section);

        Assert.NotNull(profile);
        Assert.Empty(ProfileValidator.Validate(profile));
        Assert.NotNull(section["Profile"]);
        foreach (var key in ProfileManagement.LegacyKeys)
            Assert.Null(section[key]);
        Assert.Equal(7, (int)section["RateLimit"]!);
        Assert.True((bool)section["TestMode"]!);
    }

    [Fact]
    public void Cutover_LeavesExistingProfileAndFreshInstallAlone()
    {
        var withProfile = JsonNode.Parse("""{ "Profile": { "Name": "x" }, "ApiEndpoint": "https://old" }""")!.AsObject();
        Assert.Null(ProfileManagement.CutoverLegacy(withProfile));
        Assert.Equal("https://old", (string?)withProfile["ApiEndpoint"]);

        var fresh = JsonNode.Parse("""{ "RateLimit": 10, "ApiEndpoint": "", "RestHeaders": [] }""")!.AsObject();
        Assert.Null(ProfileManagement.CutoverLegacy(fresh));
        Assert.Null(fresh["Profile"]);
    }

    [Fact]
    public void Export_EmptiesSecrets_AndImportRoundTrips()
    {
        var p = new ProviderProfile
        {
            Name = "Gw", Url = "https://gw/{Phone}", Body = "{Message}",
            Variables = { new SmsParam { Key = "User", Value = "u" }, new SmsParam { Key = "Pw", Value = "secret", IsSecret = true } },
        };
        var json = ProfileManagement.Export(p);
        Assert.DoesNotContain("secret\"", json.Replace("IsSecret", ""));

        var back = ProfileManagement.Import(json);
        Assert.Equal("u", back.Variables[0].Value);
        Assert.Equal("", back.Variables[1].Value);
        Assert.True(back.Variables[1].IsSecret);
    }

    [Fact]
    public void Import_RejectsGarbage()
    {
        Assert.Throws<InvalidOperationException>(() => ProfileManagement.Import("not json"));
        Assert.Throws<InvalidOperationException>(() => ProfileManagement.Import("{}"));
    }

    [Fact]
    public void Hash_ChangesWithAnyFieldIncludingSecrets()
    {
        var a = new ProviderProfile { Url = "https://x", Variables = { new SmsParam { Key = "K", Value = "1", IsSecret = true } } };
        var b = ProfileManagement.Clone(a);
        Assert.Equal(ProfileManagement.Hash(a), ProfileManagement.Hash(b));
        b.Variables[0].Value = "2";
        Assert.NotEqual(ProfileManagement.Hash(a), ProfileManagement.Hash(b));
    }

    [Fact]
    public void AllStarterTemplates_OnlyNeedSecretsAndUrl()
    {
        foreach (var t in ProfileTemplates.All)
        {
            var p = t.Create();
            foreach (var v in p.Variables.Where(v => v.Value.Length == 0))
                v.Value = "filled";
            Assert.True(ProfileValidator.Validate(p).Count == 0, t.Id + ": " + string.Join("; ", ProfileValidator.Validate(p)));
        }
    }
}
