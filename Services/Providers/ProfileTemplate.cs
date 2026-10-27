using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services.Providers
{
    /// <summary>Values available to templates for one recipient of one alarm.</summary>
    public record ProfileContext(
        string Message,
        string Phone,
        string? Value = null,
        string? AlarmId = null,
        string? Priority = null,
        int? GroupId = null,
        int? UserId = null,
        string? FirstName = null,
        string? LastName = null,
        string? TZ = null);

    public static class ProfileTokens
    {
        public const string Message = "Message";
        public const string Phone = "Phone";

        public static readonly string[] BuiltIns =
            { Message, Phone, "Value", "AlarmId", "Priority", "GroupId", "UserId", "FirstName", "LastName", "TZ", "Now", "Guid" };

        public static bool IsBuiltIn(string name) => BuiltIns.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    public static class Encodings
    {
        public const string Raw = "raw";
        public const string Json = "json";
        public const string Xml = "xml";
        public const string Url = "url";
        public const string Base64 = "base64";
        public static readonly string[] All = { Raw, Json, Xml, Url, Base64 };

        /// <summary>Default encoding for a body of this content type.</summary>
        public static string ForContentType(string? contentType)
        {
            var ct = (contentType ?? string.Empty).ToLowerInvariant();
            if (ct.Contains("json")) return Json;
            if (ct.Contains("xml")) return Xml;
            if (ct.Contains("x-www-form-urlencoded")) return Url;
            return Raw;
        }
    }

    public class TemplateException : Exception
    {
        public TemplateException(string message) : base(message) { }
    }

    /// <summary>
    /// Renders {Name} / {Name:encoding} tokens. {Now:format} takes a .NET date format instead of an
    /// encoding. Names are case-insensitive; an unknown name is an error, never sent as-is.
    /// </summary>
    public class ProfileTemplate
    {
        // Names start with a letter, so JSON like {"a":1} or { } is never mistaken for a token.
        private static readonly Regex Token = new(@"\{([A-Za-z][A-Za-z0-9_]*)(?::([^{}]*))?\}", RegexOptions.CultureInvariant);

        private static readonly JavaScriptEncoder JsonEncoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

        private readonly Dictionary<string, SmsParam> _variables;
        private readonly ProfileContext _context;
        private readonly string _formattedPhone;
        private readonly DateTime _now;
        private readonly string _guid = Guid.NewGuid().ToString("N");

        public ProfileTemplate(ProviderProfile profile, ProfileContext context, DateTime? now = null)
        {
            _variables = profile.Variables
                .Where(v => !string.IsNullOrWhiteSpace(v.Key))
                .GroupBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
            _context = context;
            _formattedPhone = PhoneFormatter.Format(context.Phone, profile.PhoneFormat, profile.CountryCode);
            _now = now ?? DateTime.Now;
        }

        /// <summary>Secret variable values, for masking logs and echoes.</summary>
        public IEnumerable<string> SecretValues =>
            _variables.Values.Where(v => v.IsSecret && !string.IsNullOrEmpty(v.Value)).Select(v => v.Value);

        public string Render(string? template, string defaultEncoding)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            return Token.Replace(template, m =>
            {
                var name = m.Groups[1].Value;
                var arg = m.Groups[2].Success ? m.Groups[2].Value : null;

                if (name.Equals("Now", StringComparison.OrdinalIgnoreCase))
                {
                    var text = _now.ToString(string.IsNullOrEmpty(arg) ? "yyyy-MM-ddTHH:mm:ss" : arg, CultureInfo.InvariantCulture);
                    return Encode(text, defaultEncoding);
                }

                var encoding = arg ?? defaultEncoding;
                if (!Encodings.All.Contains(encoding, StringComparer.OrdinalIgnoreCase))
                    throw new TemplateException($"Unknown encoding '{encoding}' in {m.Value}. Use raw, json, xml, url or base64.");

                return Encode(Resolve(name, m.Value), encoding.ToLowerInvariant());
            });
        }

        /// <summary>Names used in a template, for validation.</summary>
        public static IEnumerable<string> TokenNames(string? template) =>
            string.IsNullOrEmpty(template)
                ? Enumerable.Empty<string>()
                : Token.Matches(template).Select(m => m.Groups[1].Value);

        public static IEnumerable<(string Name, string? Arg)> Tokens(string? template) =>
            string.IsNullOrEmpty(template)
                ? Enumerable.Empty<(string, string?)>()
                : Token.Matches(template).Select(m => (m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : (string?)null));

        private string Resolve(string name, string token)
        {
            switch (name.ToLowerInvariant())
            {
                case "message": return _context.Message;
                case "phone": return _formattedPhone;
                case "value": return _context.Value ?? string.Empty;
                case "alarmid": return _context.AlarmId ?? string.Empty;
                case "priority": return _context.Priority ?? string.Empty;
                case "groupid": return _context.GroupId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                case "userid": return _context.UserId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                case "firstname": return _context.FirstName ?? string.Empty;
                case "lastname": return _context.LastName ?? string.Empty;
                case "tz": return _context.TZ ?? string.Empty;
                case "guid": return _guid;
            }

            if (_variables.TryGetValue(name, out var variable))
                return variable.Value;

            throw new TemplateException($"Unknown token {token}: not a built-in value and not a profile variable.");
        }

        public static string Encode(string value, string encoding) => encoding switch
        {
            Encodings.Json => JsonEncoder.Encode(value),
            Encodings.Xml => System.Security.SecurityElement.Escape(value) ?? value,
            Encodings.Url => Uri.EscapeDataString(value),
            Encodings.Base64 => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)),
            _ => value
        };
    }
}
