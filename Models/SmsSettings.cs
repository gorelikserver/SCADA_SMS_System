namespace SCADASMSSystem.Web.Models
{
    public class SmsSettings
    {
        // ─── Limits and timing ───────────────────────────────────────────────────────
        public int RateLimit { get; set; } = 10;
        public int RateWindow { get; set; } = 60;
        public int DuplicateWindow { get; set; } = 5;
        public int TimeoutSeconds { get; set; } = 30;
        public int RetryAttempts { get; set; } = 3;

        /// <summary>
        /// SCADA PCIM Object ID - varies by deployment (e.g., 198 for some installations).
        /// </summary>
        public int ScadaPcimObjectId { get; set; } = 198;

        // When true, SMS calls are routed to the in-process mock; no real SMS is sent.
        public bool TestMode { get; set; } = false;

        // Keep the last SOAP requests (secrets masked) on the Debug SOAP log page.
        public bool LogSoapXml { get; set; } = false;

        // ─── Provider profile ────────────────────────────────────────────────────────
        // The profile every alarm is sent with.
        public ProviderProfile? Profile { get; set; }

        // Work in progress from the provider editor. Never used for alarms.
        public ProviderProfile? DraftProfile { get; set; }

        // Last successful real test send of a draft; activation requires it to match the draft exactly.
        public ProfileVerification? Verification { get; set; }

        public ProfileActivation? Activation { get; set; }
    }

    public class ProfileVerification
    {
        // SHA-256 of the exact draft that was tested; any later edit invalidates it.
        public string ProfileHash { get; set; } = string.Empty;
        public DateTime VerifiedAt { get; set; }
        public string? VerifiedBy { get; set; }
        public string MaskedPhone { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string Result { get; set; } = string.Empty;
    }

    public class ProfileActivation
    {
        public DateTime ActivatedAt { get; set; }
        public string? ActivatedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? VerifiedBy { get; set; }
    }

    public class SmsParam
    {
        public string Key    { get; set; } = string.Empty;
        public string Value  { get; set; } = string.Empty;
        public bool IsSecret { get; set; } = false;
    }
}
