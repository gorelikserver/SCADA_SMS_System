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
        Task DiscardDraftAsync();
    }

    /// <summary>
    /// Draft, verify, activate. Alarms always use the active profile; a draft becomes active only
    /// after a real send to a test phone was accepted by the provider with exactly that draft.
    /// </summary>
    public class ProviderProfileService : IProviderProfileService
    {
        private readonly ISettingsStore _store;
        private readonly ISmsService _sms;
        private readonly ILogger<ProviderProfileService> _logger;

        public ProviderProfileService(ISettingsStore store, ISmsService sms, ILogger<ProviderProfileService> logger)
        {
            _store = store;
            _sms = sms;
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
                await _store.UpdateSmsSettingsAsync(section =>
                {
                    // Record only if the draft was not changed meanwhile.
                    var current = ProfileManagement.ReadProfile(section, "DraftProfile");
                    if (current == null || ProfileManagement.Hash(current) != hash)
                        return;
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
            }

            return new VerifyOutcome(result.Response.Success, result.Response.StatusCode, result.Response.Message,
                result.DisplayUrl, result.DisplayBody, Truncate(result.Response.Response, 4000));
        }

        public async Task<(bool Ok, string Message)> ActivateDraftAsync(string? user)
        {
            (bool Ok, string Message) outcome = (false, "Nothing happened.");
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

                ProfileManagement.WriteProfile(section, "Profile", draft);
                section.Remove("DraftProfile");
                section["Activation"] = JsonSerializer.SerializeToNode(new ProfileActivation
                {
                    ActivatedAt = DateTime.Now,
                    ActivatedBy = user,
                    VerifiedAt = verification.VerifiedAt,
                    VerifiedBy = verification.VerifiedBy,
                });
                outcome = (true, $"Provider profile '{draft.Name}' is now active.");
            });
            if (outcome.Ok)
                _logger.LogWarning("Provider profile activated by {User}", user);
            return outcome;
        }

        public Task DiscardDraftAsync() =>
            _store.UpdateSmsSettingsAsync(section => section.Remove("DraftProfile"));

        private static string MaskPhone(string phone) =>
            phone.Length <= 4 ? "****" : new string('*', phone.Length - 4) + phone[^4..];

        private static string? Truncate(string? s, int max) => s == null || s.Length <= max ? s : s[..max] + "…";
    }
}
