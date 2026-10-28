using System.Text.Json;
using System.Text.RegularExpressions;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services.Providers
{
    /// <summary>
    /// Checks a profile before it is saved or used: every token resolves, the request renders to a
    /// valid URL/body for a sample alarm, and acceptance can be proven from the response.
    /// </summary>
    public static class ProfileValidator
    {
        private static readonly string[] Methods = { "GET", "POST", "PUT", "PATCH" };
        private static readonly Regex VariableName = new("^[A-Za-z][A-Za-z0-9_]*$");
        private static readonly Regex HeaderName = new(@"^[!#$%&'*+\-.^_`|~0-9A-Za-z]+$");

        // Quotes, ampersand, angle brackets and Hebrew catch templates that forget to encode.
        public static readonly ProfileContext Sample = new(
            Message: "Test \"alarm\" & <check> בדיקה",
            Phone: "050-1234567",
            Value: "85.3",
            AlarmId: "SCADA-sample",
            Priority: "normal",
            GroupId: 1,
            UserId: 1,
            FirstName: "Dana",
            LastName: "Levi",
            TZ: "000000000");

        public static IReadOnlyList<string> Validate(ProviderProfile? profile)
        {
            var errors = new List<string>();
            if (profile == null)
            {
                errors.Add("No provider profile is configured.");
                return errors;
            }

            var isSoap = profile.Kind.Equals("SOAP", StringComparison.OrdinalIgnoreCase);
            if (!isSoap && !profile.Kind.Equals("REST", StringComparison.OrdinalIgnoreCase))
                errors.Add("Kind must be REST or SOAP.");
            if (!Methods.Contains(profile.Method, StringComparer.OrdinalIgnoreCase))
                errors.Add($"Method must be one of {string.Join(", ", Methods)}.");
            if (profile.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) && profile.Body.Length > 0)
                errors.Add("A GET request cannot have a body; put the values in the URL.");
            if (string.IsNullOrWhiteSpace(profile.Url))
                errors.Add("URL is required.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in profile.Variables)
            {
                if (!VariableName.IsMatch(v.Key ?? string.Empty))
                    errors.Add($"Variable name '{v.Key}' must start with a letter and use only letters, digits and _.");
                else if (ProfileTokens.IsBuiltIn(v.Key))
                    errors.Add($"Variable name '{v.Key}' is reserved for a built-in value.");
                else if (!seen.Add(v.Key))
                    errors.Add($"Variable '{v.Key}' is defined twice.");
            }

            foreach (var h in profile.Headers)
                if (!HeaderName.IsMatch(h.Name ?? string.Empty))
                    errors.Add($"Header name '{h.Name}' is not a valid HTTP header name.");

            if (!PhoneFormats.All.Contains(profile.PhoneFormat))
                errors.Add($"Phone format must be one of {string.Join(", ", PhoneFormats.All)}.");
            if (profile.PhoneFormat == PhoneFormats.Local && PhoneNumberRules.DigitsOnly(profile.CountryCode).Length == 0)
                errors.Add("Phone format Local needs a country code to strip.");

            if (!AuthTypes.All.Contains(profile.Auth.Type))
                errors.Add($"Auth type must be one of {string.Join(", ", AuthTypes.All)}.");
            if (profile.Auth.Type == AuthTypes.WsSecurity && !isSoap)
                errors.Add("WS-Security authentication needs Kind SOAP.");
            if (profile.Auth.Type is AuthTypes.Basic or AuthTypes.WsSecurity && string.IsNullOrWhiteSpace(profile.Auth.User))
                errors.Add($"{profile.Auth.Type} authentication needs a user.");
            if (profile.Auth.Type is AuthTypes.Basic or AuthTypes.Bearer or AuthTypes.WsSecurity && string.IsNullOrWhiteSpace(profile.Auth.Secret))
                errors.Add($"{profile.Auth.Type} authentication needs a password or token.");

            // Message and phone must reach the provider somewhere.
            var templates = new[] { profile.Url, profile.Body }.Concat(profile.Headers.Select(h => h.Value)).ToList();
            var used = templates.SelectMany(ProfileTemplate.TokenNames).ToList();
            foreach (var required in new[] { ProfileTokens.Message, ProfileTokens.Phone })
                if (!used.Contains(required, StringComparer.OrdinalIgnoreCase))
                    errors.Add($"The request never sends {{{required}}}: put it in the URL, body or a header.");

            foreach (var (name, arg) in templates.Append(profile.Auth.User).Append(profile.Auth.Secret).SelectMany(ProfileTemplate.Tokens))
                if (arg != null && !name.Equals("Now", StringComparison.OrdinalIgnoreCase) && !Encodings.All.Contains(arg, StringComparer.OrdinalIgnoreCase))
                    errors.Add($"Token {{{name}:{arg}}} uses an unknown encoding. Use raw, json, xml, url or base64.");

            foreach (var (rule, label) in new[] { (profile.Failure, "Failure rule"), (profile.Success, "Success rule") })
            {
                try { ResponseEvaluator.CheckRule(rule, label); }
                catch (InvalidOperationException ex) { errors.Add(ex.Message); }
            }
            if (isSoap && profile.Success.Match == MatchTypes.None)
                errors.Add("SOAP providers answer HTTP 200 even when they reject a message: set a success rule on the response body.");

            if (errors.Count > 0)
                return errors;

            // Render a sample request: catches unknown tokens and bodies that break once real values go in.
            try
            {
                using var built = ProviderRequestBuilder.Build(profile, Sample);
                var bodyText = new ProfileTemplate(profile, Sample).Render(profile.Body, Encodings.ForContentType(profile.ContentType));
                var bodyEncoding = Encodings.ForContentType(profile.ContentType);
                if (bodyText.Length > 0 && bodyEncoding == Encodings.Json)
                {
                    try { using var _ = JsonDocument.Parse(bodyText); }
                    catch (JsonException ex) { errors.Add($"Body is not valid JSON once filled in: {ex.Message}"); }
                }
                if (bodyText.Length > 0 && (bodyEncoding == Encodings.Xml || isSoap))
                {
                    var xml = ResponseEvaluator.TryParseXml(bodyText);
                    if (xml == null)
                        errors.Add("Body is not valid XML once filled in. Check that every tag is closed and namespace prefixes are declared.");
                    else if (isSoap && xml.Root?.Name.LocalName != "Envelope")
                        errors.Add("SOAP body must be a full envelope with an Envelope root element.");
                }
            }
            catch (TemplateException ex)
            {
                errors.Add(ex.Message);
            }
            catch (FormatException ex)
            {
                errors.Add($"Content type or a header value is malformed: {ex.Message}");
            }

            return errors;
        }
    }
}
