using System.Text.Json;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services.Providers
{
    public record ProfileEditorState(
        ProviderProfile? Active,
        ProviderProfile? Draft,
        ProfileVerification? Verification,
        ProfileActivation? Activation)
    {
        public bool DraftVerified =>
            Draft != null && Verification != null && Verification.ProfileHash == ProfileManagement.Hash(Draft);
    }

    public record VerifyOutcome(bool Accepted, int StatusCode, string Reason, string DisplayUrl, string DisplayBody, string? ResponseBody);

    public interface IProviderProfileService
    {
        Task<ProfileEditorState> GetStateAsync();
        Task<IReadOnlyList<string>> SaveDraftAsync(ProviderProfile posted);
        Task<VerifyOutcome> VerifyDraftAsync(string phone, string message, string? user, CancellationToken ct = default);
        Task<(bool Ok, string Message)> ActivateDraftAsync(string? user);
        Task DiscardDraftAsync(string? user);

        /// <summary>Puts a stored version into the draft. Never activates: the draft must be verified again.</summary>
        Task<(bool Ok, string Message)> RestoreVersionAsDraftAsync(int versionId, string? user);

        /// <summary>Records the active profile as a version when history has none for it (e.g. converted at startup).</summary>
        Task EnsureActiveVersionRecordedAsync();
    }

    /// <summary>
    /// Draft, verify, activate. Alarms always use the active profile; a draft becomes active only
    /// after a real send to a test phone was accepted by the provider with exactly that draft.
    /// </summary>
    public class ProviderProfileService : IProviderProfileService
    {
        private readonly ISettingsStore _store;
        private readonly ISmsService _sms;
        private readonly ISettingsHistory _history;
        private readonly ILogger<ProviderProfileService> _logger;

        public ProviderProfileService(ISettingsStore store, ISmsService sms, ISettingsHistory history, ILogger<ProviderProfileService> logger)
        {
            _store = store;
            _sms = sms;
            _history = history;
            _logger = logger;
        }

        public async Task<ProfileEditorState> GetStateAsync()
        {
            var s = await _store.ReadSmsSettingsAsync();
            return new ProfileEditorState(s.Profile, s.DraftProfile, s.Verification, s.Activation);
        }

        /// <summary>Saves the editor content as the draft (even if invalid, so no work is lost) and returns its errors.</summary>
        public async Task<IReadOnlyList<string>> SaveDraftAsync(ProviderProfile posted)
        {
            await _store.UpdateSmsSettingsAsync(section =>
            {
                ProfileManagement.RestoreMaskedSecrets(posted,
                    ProfileManagement.ReadProfile(section, "DraftProfile"),
                    ProfileManagement.ReadProfile(section, "Profile"));
                ProfileManagement.WriteProfile(section, "DraftProfile", posted);
            });
            return ProfileValidator.Validate(posted);
        }

        public async Task<VerifyOutcome> VerifyDraftAsync(string phone, string message, string? user, CancellationToken ct = default)
        {
            var draft = (await _store.ReadSmsSettingsAsync()).DraftProfile;
            if (draft == null)
                return new VerifyOutcome(false, 0, "There is no draft to verify.", "", "", null);
            if (!PhoneNumberRules.IsValid(phone))
                return new VerifyOutcome(false, 0, "Enter a valid test phone number (7-15 digits).", "", "", null);

            var errors = ProfileValidator.Validate(draft);
            if (errors.Count > 0)
                return new VerifyOutcome(false, 0, "Fix the draft first: " + string.Join("; ", errors), "", "", null);

            var result = await _sms.SendWithProfileAsync(draft, new ProfileContext(message, phone.Trim(), AlarmId: "VERIFY-" + DateTime.Now.ToString("yyyyMMddHHmmss")), ct);
            _logger.LogWarning("Provider draft verification by {User} to {Phone}: {Accepted} ({Reason})",
                user, MaskPhone(phone), result.Response.Success, result.Response.Message);

            if (result.Response.Success)
            {
                var hash = ProfileManagement.Hash(draft);
                var recorded = false;
                await _store.UpdateSmsSettingsAsync(section =>
                {
                    // Record only if the draft was not changed meanwhile.
                    var current = ProfileManagement.ReadProfile(section, "DraftProfile");
                    if (current == null || ProfileManagement.Hash(current) != hash)
                        return;
                    recorded = true;
                    section["Verification"] = JsonSerializer.SerializeToNode(new ProfileVerification
                    {
                        ProfileHash = hash,
                        VerifiedAt = DateTime.Now,
                        VerifiedBy = user,
                        MaskedPhone = MaskPhone(phone),
                        StatusCode = result.Response.StatusCode,
                        Result = result.Response.Message,
                    });
                });
                if (recorded)
                    await _history.RecordChangeAsync(SettingsAreas.Provider, SettingsActions.DraftVerified,
                        $"Verified draft {SettingsChangeSummary.Describe(draft)} with a real SMS to {MaskPhone(phone)} (HTTP {result.Response.StatusCode}).", user);
            }

            return new VerifyOutcome(result.Response.Success, result.Response.StatusCode, result.Response.Message,
                result.DisplayUrl, result.DisplayBody, Truncate(result.Response.Response, 4000));
        }

        public async Task<(bool Ok, string Message)> ActivateDraftAsync(string? user)
        {
            (bool Ok, string Message) outcome = (false, "Nothing happened.");
            ProviderProfile? previous = null, activated = null;
            ProfileActivation? previousActivation = null, activation = null;
            await _store.UpdateSmsSettingsAsync(section =>
            {
                var draft = ProfileManagement.ReadProfile(section, "DraftProfile");
                var verification = section["Verification"]?.Deserialize<ProfileVerification>();
                if (draft == null)
                {
                    outcome = (false, "There is no draft to activate.");
                    return;
                }
                var errors = ProfileValidator.Validate(draft);
                if (errors.Count > 0)
                {
                    outcome = (false, "The draft is invalid: " + string.Join("; ", errors));
                    return;
                }
                if (verification == null || verification.ProfileHash != ProfileManagement.Hash(draft))
                {
                    outcome = (false, "Verify the draft with a real test send first. Any change after a verification needs a new one.");
                    return;
                }

                previous = ProfileManagement.ReadProfile(section, "Profile");
                previousActivation = section["Activation"]?.Deserialize<ProfileActivation>();
                activated = draft;
                activation = new ProfileActivation
                {
                    ActivatedAt = DateTime.Now,
                    ActivatedBy = user,
                    VerifiedAt = verification.VerifiedAt,
                    VerifiedBy = verification.VerifiedBy,
                };

                ProfileManagement.WriteProfile(section, "Profile", draft);
                section.Remove("DraftProfile");
                section["Activation"] = JsonSerializer.SerializeToNode(activation);
                outcome = (true, $"Provider profile '{draft.Name}' is now active.");
            });
            if (outcome.Ok)
            {
                _logger.LogWarning("Provider profile activated by {User}", user);

                // The replaced profile may predate history (converted at startup, or activated before
                // history existed): keep it as a version so it can still be restored.
                if (previous != null)
                    await RecordVersionIfMissingAsync(previous, previousActivation);
                await _history.RecordVersionAsync(ToVersion(activated!, activation!, note: null));

                var changes = SettingsChangeSummary.DiffProfile(previous, activated);
                await _history.RecordChangeAsync(SettingsAreas.Provider, SettingsActions.ProfileActivated,
                    $"Activated {SettingsChangeSummary.Describe(activated!)}, verified {activation!.VerifiedAt:yyyy-MM-dd HH:mm} by {activation.VerifiedBy ?? "?"}."
                    + (previous == null ? " No profile was active before."
                        : changes == null ? $" Same as the previous profile {SettingsChangeSummary.Describe(previous)}."
                        : $" Changes from {SettingsChangeSummary.Describe(previous)}: {changes}."),
                    user);
            }
            return outcome;
        }

        public async Task DiscardDraftAsync(string? user)
        {
            ProviderProfile? discarded = null;
            await _store.UpdateSmsSettingsAsync(section =>
            {
                discarded = ProfileManagement.ReadProfile(section, "DraftProfile");
                section.Remove("DraftProfile");
            });
            if (discarded != null)
                await _history.RecordChangeAsync(SettingsAreas.Provider, SettingsActions.DraftDiscarded,
                    $"Discarded draft {SettingsChangeSummary.Describe(discarded)}.", user);
        }

        public async Task<(bool Ok, string Message)> RestoreVersionAsDraftAsync(int versionId, string? user)
        {
            var version = await _history.GetVersionAsync(versionId);
            ProviderProfile? profile = null;
            try
            {
                if (version != null)
                    profile = JsonSerializer.Deserialize<ProviderProfile>(version.ProfileJson);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Provider profile version {Id} is not readable", versionId);
            }
            if (version == null || profile == null)
                return (false, "That profile version was not found or is not readable.");

            ProviderProfile? replaced = null;
            await _store.UpdateSmsSettingsAsync(section =>
            {
                replaced = ProfileManagement.ReadProfile(section, "DraftProfile");
                ProfileManagement.WriteProfile(section, "DraftProfile", profile);
                // An old verification of the same profile must not count: going live again needs a new real send.
                section.Remove("Verification");
            });

            _logger.LogWarning("{User} restored provider profile version {Id} as the draft", user, versionId);
            await _history.RecordChangeAsync(SettingsAreas.Provider, SettingsActions.ProfileRestoredAsDraft,
                $"Restored version #{version.Id} {SettingsChangeSummary.Describe(profile)} (activated {version.ActivatedAt:yyyy-MM-dd HH:mm} by {version.ActivatedBy ?? "?"}) as the draft"
                + (replaced == null ? "." : $", replacing draft {SettingsChangeSummary.Describe(replaced)}.")
                + " It is not active until verified and activated.",
                user);
            return (true, $"Version #{version.Id} {SettingsChangeSummary.Describe(profile)} is now the draft. Verify it with a real test send, then activate it.");
        }

        public async Task EnsureActiveVersionRecordedAsync()
        {
            var s = await _store.ReadSmsSettingsAsync();
            if (s.Profile != null)
                await RecordVersionIfMissingAsync(s.Profile, s.Activation);
        }

        private async Task RecordVersionIfMissingAsync(ProviderProfile profile, ProfileActivation? activation)
        {
            if (await _history.HasVersionAsync(ProfileManagement.Hash(profile)))
                return;
            var note = activation == null
                ? "Active before history was recorded (e.g. converted from the old provider settings at startup); the time is when history first saw it."
                : "Active before history was recorded.";
            await _history.RecordVersionAsync(ToVersion(profile, activation ?? new ProfileActivation { ActivatedAt = DateTime.Now }, note));
        }

        private static ProviderProfileVersion ToVersion(ProviderProfile profile, ProfileActivation activation, string? note) => new()
        {
            ProfileName = profile.Name,
            ProfileKind = profile.Kind,
            ProfileHash = ProfileManagement.Hash(profile),
            ProfileJson = JsonSerializer.Serialize(profile),
            ActivatedAt = activation.ActivatedAt,
            ActivatedBy = activation.ActivatedBy,
            VerifiedAt = activation.VerifiedAt,
            VerifiedBy = activation.VerifiedBy,
            Note = note,
        };

        private static string MaskPhone(string phone) =>
            phone.Length <= 4 ? "****" : new string('*', phone.Length - 4) + phone[^4..];

        private static string? Truncate(string? s, int max) => s == null || s.Length <= max ? s : s[..max] + "…";
    }
}
