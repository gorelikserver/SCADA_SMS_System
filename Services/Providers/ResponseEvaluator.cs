using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services.Providers
{
    public record ProviderVerdict(bool Accepted, string Reason);

    /// <summary>
    /// Decides whether the provider accepted the message. Order: SOAP Fault, failure rule,
    /// success status codes, success body rule. Anything not positively confirmed is a failure.
    /// </summary>
    public static class ResponseEvaluator
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        public static ProviderVerdict Evaluate(ProviderProfile profile, int statusCode, string? body)
        {
            body ??= string.Empty;
            var isSoap = profile.Kind.Equals("SOAP", StringComparison.OrdinalIgnoreCase);

            XDocument? xml = null;
            if (isSoap || NeedsXml(profile.Failure) || NeedsXml(profile.Success))
            {
                xml = TryParseXml(body);
                if (isSoap && xml != null)
                {
                    var fault = xml.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Fault", StringComparison.OrdinalIgnoreCase));
                    if (fault != null)
                        return new ProviderVerdict(false, "SOAP Fault: " + FaultText(fault));
                }
            }

            if (profile.Failure.Match != MatchTypes.None &&
                StatusMatches(profile.Failure.StatusCodes, statusCode) &&
                BodyMatches(profile.Failure, body, xml, out var failureDetail))
            {
                return new ProviderVerdict(false, "Provider reported failure" + (string.IsNullOrEmpty(failureDetail) ? "" : ": " + failureDetail));
            }

            if (!StatusMatches(profile.Success.StatusCodes, statusCode))
                return new ProviderVerdict(false, $"HTTP {statusCode} is not a success status ({profile.Success.StatusCodes})");

            if (profile.Success.Match == MatchTypes.None)
                return new ProviderVerdict(true, $"HTTP {statusCode}");

            if (NeedsXml(profile.Success) && xml == null)
                return new ProviderVerdict(false, "Response is not valid XML; cannot confirm acceptance");

            return BodyMatches(profile.Success, body, xml, out _)
                ? new ProviderVerdict(true, $"HTTP {statusCode}, response confirms acceptance")
                : new ProviderVerdict(false, "Response did not confirm acceptance");
        }

        public static bool StatusMatches(string? rule, int statusCode)
        {
            if (string.IsNullOrWhiteSpace(rule))
                return true;
            foreach (var part in rule.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Length == 3 && part.EndsWith("xx", StringComparison.OrdinalIgnoreCase) && char.IsDigit(part[0]))
                {
                    if (statusCode / 100 == part[0] - '0')
                        return true;
                }
                else if (int.TryParse(part, out var code) && code == statusCode)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Throws when the rule itself is malformed; used by the profile validator.</summary>
        public static void CheckRule(ResponseRule rule, string label)
        {
            if (!MatchTypes.All.Contains(rule.Match))
                throw new InvalidOperationException($"{label}: match type must be one of {string.Join(", ", MatchTypes.All)}.");
            foreach (var part in (rule.StatusCodes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var ok = (part.Length == 3 && part.EndsWith("xx", StringComparison.OrdinalIgnoreCase) && part[0] is >= '1' and <= '5')
                         || (int.TryParse(part, out var c) && c is >= 100 and <= 599);
                if (!ok)
                    throw new InvalidOperationException($"{label}: status '{part}' is not a code (200) or class (2xx).");
            }
            if (rule.Match == MatchTypes.None)
                return;
            if (string.IsNullOrWhiteSpace(rule.Expression))
                throw new InvalidOperationException($"{label}: expression is required for {rule.Match}.");
            if (rule.Match == MatchTypes.Regex)
            {
                try { _ = new Regex(rule.Expression, RegexOptions.None, RegexTimeout); }
                catch (ArgumentException ex) { throw new InvalidOperationException($"{label}: invalid regex: {ex.Message}"); }
            }
            if (rule.Match == MatchTypes.JsonPath)
                _ = ParseJsonPath(rule.Expression, label);
        }

        private static bool NeedsXml(ResponseRule rule) => rule.Match == MatchTypes.XmlElement;

        private static bool BodyMatches(ResponseRule rule, string body, XDocument? xml, out string detail)
        {
            detail = string.Empty;
            switch (rule.Match)
            {
                case MatchTypes.Contains:
                    return body.Contains(rule.Expression, StringComparison.OrdinalIgnoreCase);

                case MatchTypes.Regex:
                    try
                    {
                        var m = Regex.Match(body, rule.Expression, RegexOptions.IgnoreCase, RegexTimeout);
                        detail = m.Success ? m.Value : string.Empty;
                        return m.Success;
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        return false;
                    }

                case MatchTypes.JsonPath:
                    var json = JsonValueAt(body, rule.Expression);
                    if (json == null)
                        return false;
                    detail = json;
                    return ValueMatches(json, rule.Expected);

                case MatchTypes.XmlElement:
                    if (xml == null)
                        return false;
                    var path = rule.Expression.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries)
                        .Select(LocalName).ToArray();
                    var matches = xml.Descendants().Where(e => PathMatches(e, path)).ToList();
                    if (rule.Expected.Length == 0)
                    {
                        // "Exists with content", e.g. an <ErrorMessage> element that is filled in.
                        var filled = matches.FirstOrDefault(e => e.Value.Trim().Length > 0 || e.Attributes().Any(a => !a.IsNamespaceDeclaration));
                        detail = filled?.Value.Trim() ?? string.Empty;
                        return filled != null;
                    }
                    var equal = matches.Where(e => ValueMatches(e.Value.Trim(), rule.Expected)).Take(2).ToList();
                    detail = equal.FirstOrDefault()?.Value.Trim() ?? string.Empty;
                    // Two matching elements are ambiguous: not proof of acceptance.
                    return equal.Count == 1;

                default:
                    return false;
            }
        }

        private static bool ValueMatches(string actual, string expected) =>
            expected.Length == 0 ? actual.Trim().Length > 0 : actual.Trim().Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase);

        private static bool PathMatches(XElement element, string[] path)
        {
            XElement? current = element;
            for (var i = path.Length - 1; i >= 0; i--)
            {
                if (current == null || !current.Name.LocalName.Equals(path[i], StringComparison.OrdinalIgnoreCase))
                    return false;
                current = current.Parent;
            }
            return true;
        }

        private static string LocalName(string segment)
        {
            var s = segment.Trim().TrimStart('<').TrimEnd('>');
            var colon = s.IndexOf(':');
            return colon >= 0 ? s[(colon + 1)..] : s;
        }

        private static string FaultText(XElement fault) =>
            fault.Descendants().FirstOrDefault(e => e.Name.LocalName is "faultstring" or "Text" or "Reason")?.Value.Trim()
            ?? fault.Value.Trim();

        public static XDocument? TryParseXml(string text)
        {
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using var reader = XmlReader.Create(new StringReader(text), settings);
                return XDocument.Load(reader);
            }
            catch (XmlException)
            {
                return null;
            }
        }

        // Minimal JSONPath: $.a.b[0].c (dots and numeric indexes only).
        private static List<object> ParseJsonPath(string path, string label)
        {
            var p = path.Trim();
            if (!p.StartsWith('$'))
                throw new InvalidOperationException($"{label}: JSON path must start with $ (e.g. $.status or $.results[0].ok).");
            var parts = new List<object>();
            foreach (Match m in Regex.Matches(p[1..], @"\.([A-Za-z0-9_\-]+)|\[(\d+)\]|\[""([^""]+)""\]"))
            {
                if (m.Groups[1].Success) parts.Add(m.Groups[1].Value);
                else if (m.Groups[2].Success) parts.Add(int.Parse(m.Groups[2].Value));
                else parts.Add(m.Groups[3].Value);
            }
            var rebuilt = string.Concat(Regex.Matches(p[1..], @"\.([A-Za-z0-9_\-]+)|\[(\d+)\]|\[""([^""]+)""\]").Select(m => m.Value));
            if (rebuilt != p[1..])
                throw new InvalidOperationException($"{label}: unsupported JSON path '{path}'. Use $.name, $.list[0], $[\"odd-name\"].");
            return parts;
        }

        private static string? JsonValueAt(string body, string path)
        {
            List<object> parts;
            try { parts = ParseJsonPath(path, "JSON path"); }
            catch (InvalidOperationException) { return null; }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var current = doc.RootElement;
                foreach (var part in parts)
                {
                    if (part is int index)
                    {
                        if (current.ValueKind != JsonValueKind.Array || index >= current.GetArrayLength()) return null;
                        current = current[index];
                    }
                    else
                    {
                        if (current.ValueKind != JsonValueKind.Object) return null;
                        var name = (string)part;
                        var found = current.EnumerateObject().FirstOrDefault(pr => pr.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (found.Value.ValueKind == JsonValueKind.Undefined) return null;
                        current = found.Value;
                    }
                }
                return current.ValueKind switch
                {
                    JsonValueKind.String => current.GetString(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => current.GetRawText()
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
