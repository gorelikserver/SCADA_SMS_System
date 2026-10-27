namespace SCADASMSSystem.Web.Models
{
    /// <summary>
    /// Everything needed to send one SMS through any REST or SOAP web service, as data.
    /// Sites are air-gapped and use different providers, so a new provider means a new profile,
    /// never a code change.
    ///
    /// Templates (Url, header values, Body, Auth fields) use {Name} or {Name:encoding} tokens.
    /// Names: built-ins (see ProfileTokens) or a <see cref="Variables"/> key.
    /// Encodings: raw, json, xml, url, base64. Without one, the encoding follows the location:
    /// url in Url, the body content type in Body (json / xml / form), raw in headers.
    /// </summary>
    public class ProviderProfile
    {
        public string Name { get; set; } = string.Empty;

        // "REST" or "SOAP". SOAP adds: SOAP Fault always means failure, WS-Security header injection.
        public string Kind { get; set; } = "REST";

        public string Method { get; set; } = "POST";
        public string Url { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/json";

        // Empty = no body (typical for GET).
        public string Body { get; set; } = string.Empty;

        public List<ProfileHeader> Headers { get; set; } = new();

        // Constants and secrets referenced as {Key}. IsSecret values never reach the browser or logs.
        public List<SmsParam> Variables { get; set; } = new();

        public ProfileAuth Auth { get; set; } = new();

        // How {Phone} is written: AsStored | DigitsOnly | E164 | International | Local.
        public string PhoneFormat { get; set; } = PhoneFormats.AsStored;

        // Country calling code without "+" (e.g. "972"), used by E164 / International / Local.
        public string CountryCode { get; set; } = string.Empty;

        // Checked first: a match means the provider rejected the message even on HTTP 2xx.
        public ResponseRule Failure { get; set; } = new() { StatusCodes = string.Empty };

        // Status codes must match, then the body rule (if any) must match.
        public ResponseRule Success { get; set; } = new();
    }

    public class ProfileHeader
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class ProfileAuth
    {
        // None | Basic | Bearer | WsSecurity (SOAP UsernameToken, PasswordText).
        public string Type { get; set; } = AuthTypes.None;

        // Templates, usually "{Username}" and "{Password}" pointing at secret variables.
        public string User { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
    }

    public class ResponseRule
    {
        // Comma list of codes or classes: "2xx", "200,201,202". Empty = any code.
        public string StatusCodes { get; set; } = "2xx";

        // None | Contains | Regex | JsonPath | XmlElement.
        public string Match { get; set; } = MatchTypes.None;

        // Contains: text. Regex: pattern. JsonPath: $.a.b[0]. XmlElement: local name or a/b/c suffix.
        public string Expression { get; set; } = string.Empty;

        // JsonPath / XmlElement: value must equal this (case-insensitive). Empty = must exist and be non-empty.
        public string Expected { get; set; } = string.Empty;
    }

    public static class PhoneFormats
    {
        public const string AsStored = "AsStored";
        public const string DigitsOnly = "DigitsOnly";
        public const string E164 = "E164";
        public const string International = "International";
        public const string Local = "Local";
        public static readonly string[] All = { AsStored, DigitsOnly, E164, International, Local };
    }

    public static class AuthTypes
    {
        public const string None = "None";
        public const string Basic = "Basic";
        public const string Bearer = "Bearer";
        public const string WsSecurity = "WsSecurity";
        public static readonly string[] All = { None, Basic, Bearer, WsSecurity };
    }

    public static class MatchTypes
    {
        public const string None = "None";
        public const string Contains = "Contains";
        public const string Regex = "Regex";
        public const string JsonPath = "JsonPath";
        public const string XmlElement = "XmlElement";
        public static readonly string[] All = { None, Contains, Regex, JsonPath, XmlElement };
    }
}
