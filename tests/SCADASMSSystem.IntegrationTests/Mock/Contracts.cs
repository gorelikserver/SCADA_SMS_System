using System.Globalization;
using System.Net.Http.Headers;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SCADASMSSystem.IntegrationTests.Mock;

/// <summary>
/// Sheba Ensemble SendSMS contract (docs/integration/sheba-ensemble-soap-spec.md).
/// Hard errors make the mock answer &lt;Success&gt;false&lt;/Success&gt; (or a SOAP Fault for
/// transport-level problems); warnings are recorded as contract gaps for the report.
/// </summary>
public sealed class SoapContract
{
    public const string SoapAction = "\"http://tempuri.org/Sheba.CoreInterfaces.BS.SMS.Direct.SendSMS\"";
    public const string SoapEnvNs = "http://schemas.xmlsoap.org/soap/envelope/";
    public const string TempuriNs = "http://tempuri.org";
    public const string WsseNs = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    public const string PasswordTextType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";
    public const int MaxMessageLength = 1000;

    public string Username { get; set; } = "scada_mock_user";
    public string Password { get; set; } = "Mock&P<ss>\"1";
    public string ExpectedMessageType { get; set; } = "SmsType1";

    public static string SuccessEnvelope { get; } = LoadFixture("sheba-sample-response-success.xml");

    public static string FailureEnvelope(string error) =>
        "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:s=\"http://www.w3.org/2001/XMLSchema\">" +
        "<SOAP-ENV:Body><SendSMSResponse xmlns=\"http://tempuri.org\"><SendSMSResult><Success>false</Success>" +
        "<ErrorMessage>" + SecurityElement.Escape(error) + "</ErrorMessage></SendSMSResult></SendSMSResponse></SOAP-ENV:Body></SOAP-ENV:Envelope>";

    public static string FaultEnvelope(string code, string text) =>
        "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\"><SOAP-ENV:Body><SOAP-ENV:Fault>" +
        "<faultcode>SOAP-ENV:" + code + "</faultcode><faultstring>" + SecurityElement.Escape(text) + "</faultstring>" +
        "</SOAP-ENV:Fault></SOAP-ENV:Body></SOAP-ENV:Envelope>";

    public static string LoadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    public void Inspect(RecordedRequest r)
    {
        var errors = r.ContractErrors;
        if (r.Method != "POST") errors.Add($"Method must be POST (got {r.Method})");

        if (!r.Headers.TryGetValue("Content-Type", out var ct) || !MediaTypeHeaderValue.TryParse(ct, out var media) ||
            !string.Equals(media.MediaType, "text/xml", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(media.CharSet?.Trim('"'), "UTF-8", StringComparison.OrdinalIgnoreCase))
            errors.Add($"Content-Type must be text/xml;charset=UTF-8 (got '{ct}')");
        else if (ct != "text/xml;charset=UTF-8")
            r.ContractWarnings.Add($"Content-Type is equivalent but not byte-identical to the spec sample: '{ct}'");

        if (!r.Headers.TryGetValue("SOAPAction", out var action) || action != SoapAction)
            errors.Add($"SOAPAction must be exactly {SoapAction} (got '{action}')");

        XDocument doc;
        try { doc = XDocument.Parse(r.Body); }
        catch (Exception ex) { errors.Add("Body is not well-formed XML: " + ex.Message); return; }

        XNamespace soap = SoapEnvNs, tem = TempuriNs, wsse = WsseNs;
        var env = doc.Root;
        if (env?.Name != soap + "Envelope") { errors.Add("Root must be soapenv:Envelope (SOAP 1.1)"); return; }

        var token = env.Element(soap + "Header")?.Element(wsse + "Security")?.Element(wsse + "UsernameToken");
        var user = token?.Element(wsse + "Username")?.Value;
        var passwordEl = token?.Element(wsse + "Password");
        r.Fields["Username"] = user;
        r.Fields["PasswordType"] = passwordEl?.Attribute("Type")?.Value;
        if (token == null) errors.Add("WS-Security UsernameToken header missing");
        if (passwordEl != null && passwordEl.Attribute("Type")?.Value != PasswordTextType)
            errors.Add("Password Type must be PasswordText");
        r.AuthOk = token != null && user == Username && passwordEl?.Value == Password;

        var send = env.Element(soap + "Body")?.Element(tem + "SendSMS");
        if (send == null) { errors.Add("Body must contain tem:SendSMS (namespace http://tempuri.org)"); return; }

        foreach (var name in new[] { "SendingSystem", "MessageType", "Message", "FirstName", "LastName", "TZ", "Phone" })
            r.Fields[name] = send.Element(tem + name)?.Value;

        r.Phone = r.Fields["Phone"];
        r.Message = r.Fields["Message"];

        if (string.IsNullOrEmpty(r.Phone)) errors.Add("Phone is required");
        else if (!Regex.IsMatch(r.Phone, "^[0-9]+$")) errors.Add($"Phone must be digits only, no dash (got '{r.Phone}')");

        if (string.IsNullOrEmpty(r.Message)) errors.Add("Message is required");
        else
        {
            var length = new StringInfo(r.Message).LengthInTextElements;
            if (length > MaxMessageLength) errors.Add($"Message is {length} characters; the limit is {MaxMessageLength}");
        }

        if (string.IsNullOrEmpty(r.Fields["MessageType"])) errors.Add("MessageType is required");
        else if (r.Fields["MessageType"] != ExpectedMessageType)
            errors.Add($"MessageType '{r.Fields["MessageType"]}' is not the type Sheba assigned ('{ExpectedMessageType}')");

        if (string.IsNullOrEmpty(r.Fields["SendingSystem"])) errors.Add("SendingSystem is required");

        foreach (var optional in new[] { "TZ", "FirstName", "LastName" })
            if (string.IsNullOrEmpty(r.Fields[optional]))
                r.ContractWarnings.Add($"{optional} is empty; the spec asks for the person's value or a fixed placeholder (sample uses 'P')");
    }

    public (int Status, string ContentType, string Body, bool Accepted) Respond(RecordedRequest r, FaultKind? fault)
    {
        const string xml = "text/xml; charset=utf-8";
        if (fault == FaultKind.AuthFailure || !r.AuthOk)
            return (500, xml, FaultEnvelope("Client", "Security token could not be authenticated or authorized"), false);
        if (r.ContractErrors.Count > 0)
            return (200, xml, FailureEnvelope(string.Join("; ", r.ContractErrors)), false);
        if (fault == FaultKind.SemanticFailure)
            return (200, xml, FailureEnvelope("Injected failure: SMS gateway refused the message"), false);
        return (200, xml, SuccessEnvelope, true);
    }
}

/// <summary>
/// Generic JSON SMS gateway (no site REST spec exists): POST JSON {to, text, reference, sender}
/// with a Bearer token; success = HTTP 200 and {"status":"ok"}.
/// </summary>
public sealed class RestContract
{
    public string Token { get; set; } = "mock-rest-token-7f3a";
    public string ExpectedSender { get; set; } = "SCADA";
    public int MaxTextLength { get; set; } = 1000;

    public void Inspect(RecordedRequest r)
    {
        var errors = r.ContractErrors;
        if (r.Method != "POST") errors.Add($"Method must be POST (got {r.Method})");

        if (!r.Headers.TryGetValue("Content-Type", out var ct) || !MediaTypeHeaderValue.TryParse(ct, out var media) ||
            !string.Equals(media.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            errors.Add($"Content-Type must be application/json (got '{ct}')");

        r.Headers.TryGetValue("Authorization", out var auth);
        r.AuthOk = auth == "Bearer " + Token;
        r.Fields["Authorization"] = auth == null ? null : auth.Split(' ')[0] + " ***";

        if (!r.Headers.TryGetValue("X-Request-Source", out var source) || source != "scada-sms")
            errors.Add($"Header X-Request-Source must be 'scada-sms' (got '{source}')");

        JsonElement root;
        try { root = JsonDocument.Parse(r.Body).RootElement; }
        catch (Exception ex) { errors.Add("Body is not valid JSON: " + ex.Message); return; }

        string? Get(string name) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        foreach (var name in new[] { "to", "text", "reference", "sender", "priority", "value" })
            r.Fields[name] = Get(name);

        r.Phone = r.Fields["to"];
        r.Message = r.Fields["text"];
        r.Reference = r.Fields["reference"];

        if (r.Phone == null || !Regex.IsMatch(r.Phone, @"^\+9725\d{8}$")) errors.Add($"'to' must be an E.164 Israeli mobile (+9725XXXXXXXX), got '{r.Phone}'");
        if (string.IsNullOrEmpty(r.Message)) errors.Add("'text' is required");
        else if (new StringInfo(r.Message).LengthInTextElements > MaxTextLength) errors.Add($"'text' exceeds {MaxTextLength} characters");
        if (string.IsNullOrEmpty(r.Reference)) errors.Add("'reference' (idempotency key) is required");
        if (r.Fields["sender"] != ExpectedSender) errors.Add($"'sender' must be '{ExpectedSender}'");
    }

    public (int Status, string ContentType, string Body, bool Accepted) Respond(RecordedRequest r, FaultKind? fault)
    {
        const string json = "application/json; charset=utf-8";
        if (fault == FaultKind.AuthFailure || !r.AuthOk)
            return (401, json, "{\"status\":\"error\",\"error\":\"invalid or missing bearer token\"}", false);
        if (r.ContractErrors.Count > 0)
            return (400, json, JsonSerializer.Serialize(new { status = "error", error = string.Join("; ", r.ContractErrors) }), false);
        if (fault == FaultKind.SemanticFailure)
            return (200, json, "{\"status\":\"error\",\"error\":\"Injected failure: gateway refused the message\"}", false);
        return (200, json, JsonSerializer.Serialize(new { status = "ok", messageId = "m-" + r.Seq }), true);
    }
}
