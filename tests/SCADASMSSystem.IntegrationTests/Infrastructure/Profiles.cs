using SCADASMSSystem.IntegrationTests.Mock;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.IntegrationTests.Infrastructure;

/// <summary>
/// Provider profiles exactly as an engineer would build them in Settings > SMS Provider.
/// They reach the app through the normal SmsSettings:Profile setting (settings.json), no product code change.
/// </summary>
public static class Profiles
{
    /// <summary>Sheba Ensemble SendSMS (SOAP 1.1 + WS-Security), body taken from the spec sample.</summary>
    public static ProviderProfile Sheba(MockSmsProvider mock) => new()
    {
        Name = "Sheba Ensemble (mock)",
        Kind = "SOAP",
        Method = "POST",
        Url = mock.SoapUrl,
        ContentType = "text/xml;charset=UTF-8",
        Headers = { new ProfileHeader { Name = "SOAPAction", Value = SoapContract.SoapAction } },
        // Spec sample envelope; the <Security> header is injected by Auth = WsSecurity.
        Body =
            "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:tem=\"http://tempuri.org\">\n" +
            "  <soapenv:Header/>\n" +
            "  <soapenv:Body>\n" +
            "    <tem:SendSMS>\n" +
            "      <tem:SendingSystem>{SendingSystem}</tem:SendingSystem>\n" +
            "      <tem:MessageType>{MessageType}</tem:MessageType>\n" +
            "      <tem:Message>{Message}</tem:Message>\n" +
            "      <tem:FirstName>{FirstName}</tem:FirstName>\n" +
            "      <tem:LastName>{LastName}</tem:LastName>\n" +
            "      <tem:TZ>{TZ}</tem:TZ>\n" +
            "      <tem:Phone>{Phone}</tem:Phone>\n" +
            "    </tem:SendSMS>\n" +
            "  </soapenv:Body>\n" +
            "</soapenv:Envelope>",
        PhoneFormat = PhoneFormats.DigitsOnly,
        Variables =
        {
            new SmsParam { Key = "Username", Value = mock.Soap.Username },
            new SmsParam { Key = "Password", Value = mock.Soap.Password, IsSecret = true },
            new SmsParam { Key = "MessageType", Value = mock.Soap.ExpectedMessageType },
            new SmsParam { Key = "SendingSystem", Value = "SCADA-SMS" },
        },
        Auth = new ProfileAuth { Type = AuthTypes.WsSecurity, User = "{Username}", Secret = "{Password}" },
        Failure = new ResponseRule { StatusCodes = "", Match = MatchTypes.XmlElement, Expression = "ErrorMessage" },
        Success = new ResponseRule { StatusCodes = "2xx", Match = MatchTypes.XmlElement, Expression = "SendSMSResult/Success", Expected = "true" },
    };

    /// <summary>Generic JSON gateway (no site REST spec): Bearer token, JSON body, success when $.status = ok.</summary>
    public static ProviderProfile JsonGateway(MockSmsProvider mock) => new()
    {
        Name = "JSON gateway (mock)",
        Kind = "REST",
        Method = "POST",
        Url = mock.RestUrl,
        ContentType = "application/json",
        Headers = { new ProfileHeader { Name = "X-Request-Source", Value = "scada-sms" } },
        Body = "{\"to\":\"{Phone}\",\"text\":\"{Message}\",\"reference\":\"{AlarmId}\",\"sender\":\"{Sender}\",\"priority\":\"{Priority}\",\"value\":\"{Value}\"}",
        PhoneFormat = PhoneFormats.E164,
        CountryCode = "972",
        Variables =
        {
            new SmsParam { Key = "Token", Value = mock.Rest.Token, IsSecret = true },
            new SmsParam { Key = "Sender", Value = mock.Rest.ExpectedSender },
        },
        Auth = new ProfileAuth { Type = AuthTypes.Bearer, Secret = "{Token}" },
        Success = new ResponseRule { StatusCodes = "2xx", Match = MatchTypes.JsonPath, Expression = "$.status", Expected = "ok" },
    };

    public static ProviderProfile For(string kind, MockSmsProvider mock) =>
        kind == "SOAP" ? Sheba(mock) : JsonGateway(mock);
}
