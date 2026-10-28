using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services.Providers
{
    /// <summary>Pure helpers for storing, masking, exporting and cutting over provider profiles.</summary>
    public static class ProfileManagement
    {
        public const string MaskedValue = "***";

        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions ExportOptions = new() { WriteIndented = true };

        // The pre-profile provider fields. The cutover removes exactly these keys.
        public static readonly string[] LegacyKeys =
        {
            "ProviderType", "ApiEndpoint", "HttpMethod", "ContentType", "ApiParams", "Username", "Password",
            "SenderName", "SoapAction", "SoapMessageType", "SoapSendingSystem", "ApiHeaders", "RestAuthType",
            "RestBearerToken", "RestApiKeyName", "RestApiKeyValue", "RestApiKeyLocation", "RestSuccessPattern",
            "RestFailurePattern", "SoapAuthType", "SoapBodyTemplate", "SoapEnvelopeNamespaces", "SoapSuccessPattern",
            "SoapErrorPattern", "SoapParams", "RestHeaders", "RestBodyParams", "SoapPlaceholders", "ProfileSource",
        };

        public static ProviderProfile Clone(ProviderProfile profile) =>
            JsonSerializer.Deserialize<ProviderProfile>(JsonSerializer.Serialize(profile))!;

        /// <summary>Identifies the exact profile that was verified, secrets included.</summary>
        public static string Hash(ProviderProfile profile) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(profile))));

        /// <summary>Copy for the browser: secret variable values become ***.</summary>
        public static ProviderProfile MaskSecrets(ProviderProfile profile)
        {
            var copy = Clone(profile);
            foreach (var v in copy.Variables.Where(v => v.IsSecret && !string.IsNullOrEmpty(v.Value)))
                v.Value = MaskedValue;
            return copy;
        }

        /// <summary>
        /// A secret posted as *** (or empty) keeps the stored value of the same variable, taken from
        /// the draft first, then the active profile.
        /// </summary>
        public static void RestoreMaskedSecrets(ProviderProfile posted, params ProviderProfile?[] sources)
        {
            foreach (var v in posted.Variables.Where(v => v.IsSecret && (string.IsNullOrEmpty(v.Value) || v.Value == MaskedValue)))
            {
                var stored = sources
                    .Where(s => s != null)
                    .SelectMany(s => s!.Variables)
                    .FirstOrDefault(s => s.Key.Equals(v.Key, StringComparison.OrdinalIgnoreCase) && s.Value != MaskedValue);
                v.Value = stored?.Value ?? string.Empty;
            }
        }

        /// <summary>Export file: secrets emptied so the file can travel between sites safely.</summary>
        public static string Export(ProviderProfile profile)
        {
            var copy = Clone(profile);
            foreach (var v in copy.Variables.Where(v => v.IsSecret))
                v.Value = string.Empty;
            return JsonSerializer.Serialize(copy, ExportOptions);
        }

        public static ProviderProfile Import(string json)
        {
            ProviderProfile? profile;
            try
            {
                profile = JsonSerializer.Deserialize<ProviderProfile>(json, ReadOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("The file is not a provider profile (invalid JSON): " + ex.Message);
            }
            if (profile == null || string.IsNullOrWhiteSpace(profile.Url) && string.IsNullOrWhiteSpace(profile.Body))
                throw new InvalidOperationException("The file is not a provider profile (no URL or body).");
            return profile;
        }

        public static ProviderProfile? ReadProfile(JsonObject section, string key)
        {
            try { return section[key]?.Deserialize<ProviderProfile>(ReadOptions); }
            catch (JsonException) { return null; }
        }

        public static void WriteProfile(JsonObject section, string key, ProviderProfile? profile) =>
            section[key] = profile == null ? null : JsonSerializer.SerializeToNode(profile);

        /// <summary>
        /// Hard cutover for a site upgraded from the pre-profile version: converts its old provider
        /// fields into Profile and removes them from <paramref name="section"/> (the UI settings file).
        /// <paramref name="effective"/> is the old configuration as the app actually saw it (all
        /// settings files and environment variables), so the profile matches what was in use.
        /// Returns the new profile, or null when nothing changed (a profile already exists, or no
        /// provider was configured: a fresh install).
        /// </summary>
        public static ProviderProfile? CutoverLegacy(JsonObject section, LegacySmsSettings? effective = null)
        {
            if (section["Profile"] != null)
                return null;

            var legacy = effective ?? section.Deserialize<LegacySmsSettings>(ReadOptions) ?? new LegacySmsSettings();
            if (string.IsNullOrWhiteSpace(legacy.ApiEndpoint))
                return null;

            var profile = LegacyProfileConverter.Convert(legacy);
            WriteProfile(section, "Profile", profile);
            foreach (var key in LegacyKeys)
                section.Remove(key);
            return profile;
        }
    }
}
