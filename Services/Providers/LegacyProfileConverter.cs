using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services.Providers
{
    /// <summary>
    /// Builds a <see cref="ProviderProfile"/> from the pre-profile SmsSettings fields, reproducing
    /// what the old REST/SOAP send code did. Used once per site at startup, and on every save from
    /// the current Settings page until it edits profiles directly.
    /// </summary>
    public static class LegacyProfileConverter
    {
        // Old dynamic keywords -> built-in tokens or variables.
        private static readonly Dictionary<string, string> Keywords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["message"] = "Message", ["msg"] = "Message", ["text"] = "Message", ["body"] = "Message",
            ["phone"] = "Phone", ["phonenumber"] = "Phone", ["sendtophonenumbers"] = "Phone", ["mobile"] = "Phone", ["number"] = "Phone",
            ["tz"] = "TZ", ["firstname"] = "FirstName", ["lastname"] = "LastName",
            ["group_id"] = "GroupId", ["groupid"] = "GroupId",
            ["alarm_id"] = "AlarmId", ["alarmid"] = "AlarmId",
            ["priority"] = "Priority", ["value"] = "Value",
            ["username"] = "Username", ["user"] = "Username", ["userid"] = "Username",
            ["password"] = "Password", ["pass"] = "Password",
            ["sender_name"] = "SenderName", ["sendername"] = "SenderName", ["sender"] = "SenderName", ["from"] = "SenderName",
            ["sendingsystem"] = "SendingSystem", ["messagetype"] = "MessageType",
        };

        public static ProviderProfile Convert(SmsSettings s) =>
            string.Equals(s.ProviderType, "SOAP", StringComparison.OrdinalIgnoreCase) ? ConvertSoap(s) : ConvertRest(s);

        private static ProviderProfile ConvertRest(SmsSettings s)
        {
            var profile = new ProviderProfile
            {
                Name = "Converted REST provider",
                Kind = "REST",
                Method = string.IsNullOrWhiteSpace(s.HttpMethod) ? "POST" : s.HttpMethod.Trim().ToUpperInvariant(),
                ContentType = string.IsNullOrWhiteSpace(s.ContentType) ? "application/x-www-form-urlencoded" : s.ContentType.Trim(),
            };
            var vars = new VariableSet(profile, s);

            var parameters = s.RestBodyParams.Count > 0
                ? s.RestBodyParams.Select(p => (p.Key, p.Value, p.IsSecret)).ToList()
                : ParseMap(s.ApiParams).Select(kv => (kv.Key, kv.Value, false)).ToList();

            // Each param becomes a token; a literal value becomes a variable so it is never re-parsed.
            var fields = parameters
                .Where(p => !string.IsNullOrWhiteSpace(p.Key))
                .Select(p => (p.Key, Token: vars.TokenFor(p.Key, p.Value, p.Item3)))
                .ToList();

            var isGet = profile.Method == "GET";
            var json = profile.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase);
            if (isGet)
            {
                profile.ContentType = string.Empty;
                profile.Url = AppendQuery(s.ApiEndpoint, fields.Select(f => $"{Uri.EscapeDataString(f.Key)}={f.Token}"));
            }
            else if (json)
            {
                profile.Url = s.ApiEndpoint;
                profile.Body = "{" + string.Join(",", fields.Select(f => $"{JsonSerializer.Serialize(f.Key)}:\"{f.Token}\"")) + "}";
            }
            else
            {
                profile.Url = s.ApiEndpoint;
                profile.Body = string.Join("&", fields.Select(f => $"{Uri.EscapeDataString(f.Key)}={f.Token}"));
            }

            var headers = s.RestHeaders.Count > 0
                ? s.RestHeaders.Select(h => (h.Key, h.Value, h.IsSecret)).ToList()
                : ParseMap(s.ApiHeaders).Select(kv => (kv.Key, kv.Value, false)).ToList();
            foreach (var (name, value, secret) in headers.Where(h => !string.IsNullOrWhiteSpace(h.Key)))
                profile.Headers.Add(new ProfileHeader { Name = name, Value = secret ? "{" + vars.Literal("Header_" + name, value, true) + "}" : vars.Text("Header_" + name, value) });

            switch ((s.RestAuthType ?? "None").Trim().ToLowerInvariant())
            {
                case "basic" when !string.IsNullOrEmpty(s.Username):
                    profile.Auth = new ProfileAuth { Type = AuthTypes.Basic, User = "{" + vars.Username() + "}", Secret = "{" + vars.Password() + "}" };
                    break;
                case "bearer" when !string.IsNullOrEmpty(s.RestBearerToken):
                    profile.Auth = new ProfileAuth { Type = AuthTypes.Bearer, Secret = "{" + vars.Literal("BearerToken", s.RestBearerToken, true) + "}" };
                    break;
                case "apikey" when !string.IsNullOrEmpty(s.RestApiKeyName):
                    var keyToken = "{" + vars.Literal("ApiKey", s.RestApiKeyValue ?? string.Empty, true) + "}";
                    if ((s.RestApiKeyLocation ?? "Header").Trim().Equals("Query", StringComparison.OrdinalIgnoreCase))
                        profile.Url = AppendQuery(profile.Url, new[] { $"{Uri.EscapeDataString(s.RestApiKeyName)}={keyToken}" });
                    else
                        profile.Headers.Add(new ProfileHeader { Name = s.RestApiKeyName, Value = keyToken });
                    break;
            }

            profile.Failure = string.IsNullOrEmpty(s.RestFailurePattern)
                ? new ResponseRule { StatusCodes = string.Empty }
                : new ResponseRule { StatusCodes = "2xx", Match = MatchTypes.Contains, Expression = s.RestFailurePattern };
            profile.Success = string.IsNullOrEmpty(s.RestSuccessPattern)
                ? new ResponseRule { StatusCodes = "2xx" }
                : new ResponseRule { StatusCodes = "2xx", Match = MatchTypes.Contains, Expression = s.RestSuccessPattern };
            return profile;
        }

        private static ProviderProfile ConvertSoap(SmsSettings s)
        {
            var profile = new ProviderProfile
            {
                Name = "Converted SOAP provider",
                Kind = "SOAP",
                Method = "POST",
                Url = s.ApiEndpoint,
                ContentType = "text/xml; charset=utf-8",
            };
            var vars = new VariableSet(profile, s);

            // Placeholder {Key} -> keyword: replace each with the matching built-in token or variable.
            var body = s.SoapBodyTemplate ?? string.Empty;
            var map = s.SoapPlaceholders.Count > 0
                ? s.SoapPlaceholders.Select(p => (p.Key, p.Value, p.IsSecret)).ToList()
                : ParseMap(s.SoapParams).Select(kv => (kv.Key, kv.Value, false)).ToList();
            if (map.Count == 0)
            {
                // Oldest form: the template already uses fixed token names.
                map = new[] { "Message", "Phone", "Username", "Password", "SenderName", "FirstName", "LastName", "TZ", "SendingSystem", "MessageType" }
                    .Select(k => (k, k, false)).ToList();
            }
            foreach (var (key, value, secret) in map.Where(m => !string.IsNullOrWhiteSpace(m.Key)))
                body = body.Replace("{" + key + "}", vars.TokenFor(key, value, secret), StringComparison.Ordinal);

            var ns = string.IsNullOrWhiteSpace(s.SoapEnvelopeNamespaces) ? string.Empty : " " + s.SoapEnvelopeNamespaces.Trim();
            profile.Body =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                $"<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"{ns}>\n" +
                "  <soapenv:Header/>\n" +
                "  <soapenv:Body>\n" + body + "\n  </soapenv:Body>\n" +
                "</soapenv:Envelope>";

            if (!string.IsNullOrWhiteSpace(s.SoapAction))
                profile.Headers.Add(new ProfileHeader { Name = "SOAPAction", Value = "\"" + vars.Text("SoapAction", s.SoapAction) + "\"" });

            var auth = (s.SoapAuthType ?? "WSSecurity").Trim();
            if (auth.Equals("WSSecurity", StringComparison.OrdinalIgnoreCase))
                profile.Auth = new ProfileAuth { Type = AuthTypes.WsSecurity, User = "{" + vars.Username() + "}", Secret = "{" + vars.Password() + "}" };
            else if (auth.Equals("HttpBasic", StringComparison.OrdinalIgnoreCase))
                profile.Auth = new ProfileAuth { Type = AuthTypes.Basic, User = "{" + vars.Username() + "}", Secret = "{" + vars.Password() + "}" };

            profile.Success = SoapRule(s.SoapSuccessPattern, expectValue: true) ?? new ResponseRule { StatusCodes = "2xx" };
            profile.Failure = SoapRule(s.SoapErrorPattern, expectValue: false) is { } failure
                ? new ResponseRule { StatusCodes = string.Empty, Match = failure.Match, Expression = failure.Expression, Expected = failure.Expected }
                : new ResponseRule { StatusCodes = string.Empty };
            return profile;
        }

        // "<Success>true</Success>" -> element Success equals "true"; "<ErrorMessage>" -> element exists with content.
        private static ResponseRule? SoapRule(string? pattern, bool expectValue)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return null;
            var p = pattern.Trim();
            if (!p.StartsWith('<'))
                return new ResponseRule { StatusCodes = "2xx", Match = MatchTypes.Contains, Expression = p };

            try
            {
                var element = XElement.Parse(p);
                return new ResponseRule { StatusCodes = "2xx", Match = MatchTypes.XmlElement, Expression = element.Name.LocalName, Expected = expectValue ? element.Value.Trim() : string.Empty };
            }
            catch (System.Xml.XmlException)
            {
                // An opening tag only, possibly prefixed: "<ns:ErrorMessage>".
                var name = p.TrimStart('<').TrimEnd('>').Split(' ', '/')[0];
                if (name.Contains(':')) name = name[(name.IndexOf(':') + 1)..];
                return new ResponseRule { StatusCodes = "2xx", Match = MatchTypes.XmlElement, Expression = name };
            }
        }

        private static Dictionary<string, string> ParseMap(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new();
            try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); }
            catch (JsonException) { return new(); }
        }

        private static string AppendQuery(string url, IEnumerable<string> pairs)
        {
            var query = string.Join("&", pairs);
            if (query.Length == 0) return url;
            return url + (url.Contains('?') ? "&" : "?") + query;
        }

        /// <summary>Collects variables while converting; names stay readable and unique.</summary>
        private sealed class VariableSet
        {
            private readonly ProviderProfile _profile;
            private readonly SmsSettings _s;
            private readonly bool _soap;

            public VariableSet(ProviderProfile profile, SmsSettings s)
            {
                _profile = profile;
                _s = s;
                _soap = profile.Kind == "SOAP";
            }

            /// <summary>Literal text for a template; moved into a variable if it looks like a token.</summary>
            public string Text(string preferredName, string value) =>
                ProfileTemplate.TokenNames(value).Any() ? "{" + Literal(preferredName, value, false) + "}" : value;

            public string TokenFor(string key, string? value, bool secret)
            {
                value ??= string.Empty;
                if (Keywords.TryGetValue(value.Trim(), out var name))
                {
                    return name switch
                    {
                        "Username" => "{" + Username() + "}",
                        "Password" => "{" + Password() + "}",
                        // The old REST code defaulted an empty sender to "SCADA"; the old SOAP code sent it empty.
                        "SenderName" => "{" + Literal("SenderName", string.IsNullOrEmpty(_s.SenderName) && !_soap ? "SCADA" : _s.SenderName ?? string.Empty, false) + "}",
                        "SendingSystem" => "{" + Literal("SendingSystem", string.IsNullOrEmpty(_s.SoapSendingSystem) ? "SCADA" : _s.SoapSendingSystem, false) + "}",
                        "MessageType" => "{" + Literal("MessageType", string.IsNullOrEmpty(_s.SoapMessageType) ? "SmsType1" : _s.SoapMessageType, false) + "}",
                        _ => "{" + name + "}"
                    };
                }
                // A fixed value: keep it exactly, as a variable named after the field.
                return "{" + Literal(key, value, secret) + "}";
            }

            public string Username() => Literal("Username", _s.Username ?? string.Empty, false);
            public string Password() => Literal("Password", _s.Password ?? string.Empty, true);

            public string Literal(string preferredName, string value, bool secret)
            {
                var baseName = Sanitize(preferredName);
                var existing = _profile.Variables.FirstOrDefault(v => v.Key.Equals(baseName, StringComparison.OrdinalIgnoreCase));
                if (existing != null && existing.Value == value)
                {
                    existing.IsSecret |= secret;
                    return existing.Key;
                }

                var name = baseName;
                for (var i = 2; _profile.Variables.Any(v => v.Key.Equals(name, StringComparison.OrdinalIgnoreCase)) || ProfileTokens.IsBuiltIn(name); i++)
                    name = baseName + i;
                _profile.Variables.Add(new SmsParam { Key = name, Value = value, IsSecret = secret });
                return name;
            }

            private static string Sanitize(string name)
            {
                var sb = new StringBuilder();
                foreach (var c in name)
                    sb.Append(char.IsAsciiLetterOrDigit(c) || c == '_' ? c : '_');
                var result = sb.ToString().Trim('_');
                if (result.Length == 0 || !char.IsAsciiLetter(result[0]))
                    result = "Var_" + result;
                return result;
            }
        }
    }
}
