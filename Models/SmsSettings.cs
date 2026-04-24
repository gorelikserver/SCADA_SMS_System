namespace SCADASMSSystem.Web.Models
{
    public class SmsSettings
    {
        // "REST" or "SOAP"
        public string ProviderType { get; set; } = "REST";

        public string ApiEndpoint { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = "POST";
        public string ContentType { get; set; } = "application/x-www-form-urlencoded";
        public string ApiParams { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public int RateLimit { get; set; } = 10;
        public int RateWindow { get; set; } = 60;
        public int DuplicateWindow { get; set; } = 5;
        public int TimeoutSeconds { get; set; } = 30;
        public int RetryAttempts { get; set; } = 3;

        // SOAP-specific (ignored when ProviderType = "REST")
        public string SoapAction { get; set; } = "http://tempuri.org/Sheba.CoreInterfaces.BS.SMS.Direct.SendSMS";
        public string SoapMessageType { get; set; } = "SmsType1";
        public string SoapSendingSystem { get; set; } = "SCADA";

        /// <summary>
        /// SCADA PCIM Object ID - varies by deployment (e.g., 198 for some installations).
        /// </summary>
        public int ScadaPcimObjectId { get; set; } = 198;

        // ─── REST extras ─────────────────────────────────────────────────────────
        // JSON dict of extra HTTP request headers: {"X-API-Key": "abc", "Authorization": "Bearer xyz"}
        public string ApiHeaders { get; set; } = string.Empty;

        // REST authentication method applied automatically by SmsService:
        //   "None"   - no auth header injected
        //   "Basic"  - Authorization: Basic base64(Username:Password)
        //   "Bearer" - Authorization: Bearer {RestBearerToken}
        //   "ApiKey" - {RestApiKeyName} sent as header or query (per RestApiKeyLocation)
        public string RestAuthType { get; set; } = "None";

        // Bearer token for RestAuthType="Bearer" (stored in plain text in appsettings.json,
        // same trust boundary as Username/Password)
        public string RestBearerToken { get; set; } = string.Empty;

        // API-key header/query name (e.g. "X-API-Key", "api_key")
        public string RestApiKeyName { get; set; } = string.Empty;

        // API-key value
        public string RestApiKeyValue { get; set; } = string.Empty;

        // "Header" (default) or "Query"
        public string RestApiKeyLocation { get; set; } = "Header";

        // If set, response body must contain this string for REST call to count as success
        public string RestSuccessPattern { get; set; } = string.Empty;

        // If set, response body containing this string is treated as failure even on 2xx
        public string RestFailurePattern { get; set; } = string.Empty;

        // ─── SOAP extras ─────────────────────────────────────────────────────────
        // "WSSecurity" (default) | "HttpBasic" | "None"
        public string SoapAuthType { get; set; } = "WSSecurity";

        // Full SOAP body element XML with placeholders:
        // {Message} {Phone} {Username} {Password} {SenderName}
        // {FirstName} {LastName} {TZ} {SendingSystem} {MessageType}
        // Empty = use legacy hardcoded Sheba template
        public string SoapBodyTemplate { get; set; } = string.Empty;

        // Extra xmlns declarations appended to soapenv:Envelope opening tag
        public string SoapEnvelopeNamespaces { get; set; } = string.Empty;

        // String that must appear in SOAP response for success
        public string SoapSuccessPattern { get; set; } = "<Success>true</Success>";

        // Opening tag of error element in SOAP response
        public string SoapErrorPattern { get; set; } = "<ErrorMessage>";

        // JSON key-value map for SOAP body placeholder resolution.
        // Key = placeholder token (without braces, e.g. "Message", "Phone", "TZ").
        // Value = dynamic keyword (message/phone/tz/username/password/sender_name/
        //         firstName/lastName/sendingSystem/messageType) or literal static value.
        // Empty = use hardcoded placeholder replacements (backward-compatible).
        public string SoapParams { get; set; } = string.Empty;

        // ─── Test mode ───────────────────────────────────────────────────────────
        // When true, SMS calls are routed to in-process mock at /mock/sms/*
        public bool TestMode { get; set; } = false;
    }
}