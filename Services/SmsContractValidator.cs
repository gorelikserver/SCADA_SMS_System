using System.Text.RegularExpressions;
using System.Xml.Linq;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services;

public static class SmsContractValidator
{
    private static readonly HashSet<string> PhoneKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "phone", "phonenumber", "sendtophonenumbers", "mobile", "number" };
    private static readonly HashSet<string> MessageKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "message", "msg", "text", "body" };
    private static readonly HashSet<string> Methods = new(StringComparer.OrdinalIgnoreCase)
        { "GET", "POST", "PUT", "PATCH" };

    public static IReadOnlyList<string> Validate(SmsSettings settings)
    {
        var errors = new List<string>();
        if (!Uri.TryCreate(settings.ApiEndpoint, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
            errors.Add("Provider endpoint must be an absolute HTTP or HTTPS URL.");

        if (settings.ProviderType.Equals("SOAP", StringComparison.OrdinalIgnoreCase))
            ValidateSoap(settings, errors);
        else if (settings.ProviderType.Equals("REST", StringComparison.OrdinalIgnoreCase))
            ValidateRest(settings, errors);
        else
            errors.Add("Provider type must be REST or SOAP.");

        return errors;
    }

    private static void ValidateRest(SmsSettings settings, List<string> errors)
    {
        if (!Methods.Contains(settings.HttpMethod))
            errors.Add("REST method must be GET, POST, PUT, or PATCH.");
        RequireSingleMapping(settings.RestBodyParams, PhoneKeywords, "phone", "REST", errors);
        RequireSingleMapping(settings.RestBodyParams, MessageKeywords, "message", "REST", errors);
    }

    private static void ValidateSoap(SmsSettings settings, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(settings.SoapAction))
            errors.Add("SOAP action is required.");
        if (string.IsNullOrWhiteSpace(settings.SoapBodyTemplate))
            errors.Add("SOAP body template is required.");
        if (string.IsNullOrWhiteSpace(settings.SoapSuccessPattern))
            errors.Add("SOAP success pattern is required.");

        if (settings.SoapPlaceholders.Count > 0)
        {
            RequireSoapMapping(settings, PhoneKeywords, "phone", errors);
            RequireSoapMapping(settings, MessageKeywords, "message", errors);
        }
        else if (!string.IsNullOrWhiteSpace(settings.SoapBodyTemplate))
        {
            RequireToken(settings.SoapBodyTemplate, "Phone", "phone", errors);
            RequireToken(settings.SoapBodyTemplate, "Message", "message", errors);
        }

        if (!string.IsNullOrWhiteSpace(settings.SoapBodyTemplate))
        {
            try
            {
                var namespaces = settings.SoapEnvelopeNamespaces?.Trim();
                XDocument.Parse($"<Envelope {(string.IsNullOrEmpty(namespaces) ? "" : namespaces)}><Body>{settings.SoapBodyTemplate}</Body></Envelope>");
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or ArgumentException)
            {
                errors.Add($"SOAP body template or namespace declarations are invalid XML: {ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.SoapSuccessPattern) && settings.SoapSuccessPattern.TrimStart().StartsWith('<'))
        {
            try { XElement.Parse(settings.SoapSuccessPattern); }
            catch (System.Xml.XmlException ex) { errors.Add($"SOAP success pattern is invalid XML: {ex.Message}"); }
        }
    }

    private static void RequireSoapMapping(SmsSettings settings, HashSet<string> keywords, string name, List<string> errors)
    {
        var matches = settings.SoapPlaceholders.Where(p => keywords.Contains(p.Value ?? "")).ToList();
        if (matches.Count != 1)
        {
            errors.Add($"SOAP contract must map exactly one placeholder to '{name}'.");
            return;
        }
        RequireToken(settings.SoapBodyTemplate, matches[0].Key, name, errors);
    }

    private static void RequireSingleMapping(IEnumerable<SmsParam> mappings, HashSet<string> keywords, string name, string provider, List<string> errors)
    {
        if (mappings.Count(p => keywords.Contains(p.Value ?? "")) != 1)
            errors.Add($"{provider} contract must map exactly one parameter to '{name}'.");
    }

    private static void RequireToken(string template, string key, string name, List<string> errors)
    {
        var token = $"{{{key}}}";
        if (Regex.Matches(template, Regex.Escape(token), RegexOptions.CultureInvariant).Count != 1)
            errors.Add($"SOAP body template must contain exactly one {token} token for '{name}'.");
    }
}
