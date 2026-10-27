using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services.Providers
{
    /// <summary>The HTTP request for one recipient, plus a copy safe to show and log (secrets masked).</summary>
    public sealed record BuiltRequest(HttpRequestMessage Request, string DisplayUrl, string DisplayBody) : IDisposable
    {
        public void Dispose() => Request.Dispose();
    }

    public static class ProviderRequestBuilder
    {
        private const string WsseNs = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        private const string PasswordTextType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";

        public static BuiltRequest Build(ProviderProfile profile, ProfileContext context, DateTime? now = null)
        {
            var template = new ProfileTemplate(profile, context, now);

            var url = template.Render(profile.Url, Encodings.Url);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new TemplateException($"Provider URL is not an absolute http(s) URL: {Mask(url, template.SecretValues)}");

            var body = template.Render(profile.Body, Encodings.ForContentType(profile.ContentType));
            var authUser = template.Render(profile.Auth.User, Encodings.Raw);
            var authSecret = template.Render(profile.Auth.Secret, Encodings.Raw);
            var secrets = template.SecretValues.Append(authSecret).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();

            if (profile.Auth.Type == AuthTypes.WsSecurity)
                body = InjectWsSecurity(body, authUser, authSecret);

            var request = new HttpRequestMessage(new HttpMethod(profile.Method.ToUpperInvariant()), uri);
            if (body.Length > 0)
            {
                request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
                request.Content.Headers.ContentType = ContentTypeHeader(profile.ContentType);
            }

            foreach (var header in profile.Headers.Where(h => !string.IsNullOrWhiteSpace(h.Name)))
            {
                var value = template.Render(header.Value, Encodings.Raw);
                if (header.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    continue; // set from ContentType
                if (!request.Headers.TryAddWithoutValidation(header.Name, value))
                    request.Content?.Headers.TryAddWithoutValidation(header.Name, value);
            }

            switch (profile.Auth.Type)
            {
                case AuthTypes.Basic:
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{authUser}:{authSecret}")));
                    break;
                case AuthTypes.Bearer:
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authSecret);
                    break;
            }

            return new BuiltRequest(request, Mask(url, secrets), Mask(body, secrets));
        }

        private static MediaTypeHeaderValue ContentTypeHeader(string? contentType)
        {
            var value = string.IsNullOrWhiteSpace(contentType) ? "text/plain" : contentType;
            var header = MediaTypeHeaderValue.Parse(value);
            header.CharSet ??= "utf-8";
            return header;
        }

        /// <summary>Adds a UsernameToken (PasswordText) to the envelope's Header, whatever its prefix or SOAP version.</summary>
        private static string InjectWsSecurity(string envelope, string user, string password)
        {
            var doc = ResponseEvaluator.TryParseXml(envelope)
                      ?? throw new TemplateException("WS-Security needs the body to be a valid SOAP envelope (XML).");
            var root = doc.Root!;
            if (!root.Name.LocalName.Equals("Envelope", StringComparison.Ordinal))
                throw new TemplateException("WS-Security needs the body's root element to be a SOAP Envelope.");

            var soapNs = root.Name.Namespace;
            var header = root.Element(soapNs + "Header");
            if (header == null)
            {
                header = new XElement(soapNs + "Header");
                root.AddFirst(header);
            }

            XNamespace wsse = WsseNs;
            header.Add(new XElement(wsse + "Security",
                new XElement(wsse + "UsernameToken",
                    new XElement(wsse + "Username", user),
                    new XElement(wsse + "Password", new XAttribute("Type", PasswordTextType), password))));

            var declaration = doc.Declaration != null ? doc.Declaration + "\n" : string.Empty;
            return declaration + root.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>Replaces each secret, and its encoded forms, with ***.</summary>
        public static string Mask(string text, IEnumerable<string> secrets)
        {
            foreach (var secret in secrets.Where(s => s.Length > 0).OrderByDescending(s => s.Length))
            {
                foreach (var encoding in Encodings.All)
                {
                    var encoded = ProfileTemplate.Encode(secret, encoding);
                    if (encoded.Length > 0)
                        text = text.Replace(encoded, "***", StringComparison.Ordinal);
                }
            }
            return text;
        }
    }
}
