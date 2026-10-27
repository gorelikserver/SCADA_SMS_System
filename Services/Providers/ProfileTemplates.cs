using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services.Providers
{
    public record ProfileStarter(string Id, string Title, string Description, Func<ProviderProfile> Create);

    /// <summary>
    /// Starting points shipped with the installer (sites are air-gapped, nothing is downloaded).
    /// Each one validates once its URL and secrets are filled in.
    /// </summary>
    public static class ProfileTemplates
    {
        public static readonly IReadOnlyList<ProfileStarter> All = new[]
        {
            new ProfileStarter("json-bearer", "JSON POST, Bearer token",
                "Modern REST gateway: JSON body, token in the Authorization header, success when $.status is ok.",
                () => new ProviderProfile
                {
                    Name = "JSON gateway",
                    Kind = "REST",
                    Method = "POST",
                    Url = "https://sms-gateway.local/api/v1/messages",
                    ContentType = "application/json",
                    Body = "{\n  \"to\": \"{Phone}\",\n  \"text\": \"{Message}\",\n  \"reference\": \"{AlarmId}\"\n}",
                    PhoneFormat = PhoneFormats.E164,
                    CountryCode = "972",
                    Variables = { new SmsParam { Key = "Token", Value = "", IsSecret = true } },
                    Auth = new ProfileAuth { Type = AuthTypes.Bearer, Secret = "{Token}" },
                    Success = new ResponseRule { StatusCodes = "2xx", Match = MatchTypes.JsonPath, Expression = "$.status", Expected = "ok" },
                }),
            new ProfileStarter("form-post", "Form POST, user and password in body",
                "Classic HTTP gateway: application/x-www-form-urlencoded fields, any 2xx without an error text.",
                () => new ProviderProfile
                {
                    Name = "Form gateway",
                    Kind = "REST",
                    Method = "POST",
                    Url = "https://sms-gateway.local/send",
                    ContentType = "application/x-www-form-urlencoded",
                    Body = "user={User}&password={Password}&from={Sender}&to={Phone}&text={Message}",
                    PhoneFormat = PhoneFormats.International,
                    CountryCode = "972",
                    Variables =
                    {
                        new SmsParam { Key = "User", Value = "" },
                        new SmsParam { Key = "Password", Value = "", IsSecret = true },
                        new SmsParam { Key = "Sender", Value = "SCADA" },
                    },
                    Failure = new ResponseRule { StatusCodes = "", Match = MatchTypes.Contains, Expression = "ERROR" },
                }),
            new ProfileStarter("get-query", "GET with query string",
                "Everything in the URL. Values are URL-encoded automatically.",
                () => new ProviderProfile
                {
                    Name = "GET gateway",
                    Kind = "REST",
                    Method = "GET",
                    Url = "https://sms-gateway.local/send?user={User}&pass={Password}&to={Phone}&msg={Message}",
                    ContentType = "",
                    PhoneFormat = PhoneFormats.DigitsOnly,
                    Variables =
                    {
                        new SmsParam { Key = "User", Value = "" },
                        new SmsParam { Key = "Password", Value = "", IsSecret = true },
                    },
                    Success = new ResponseRule { StatusCodes = "200", Match = MatchTypes.Regex, Expression = "^\\s*OK" },
                }),
            new ProfileStarter("soap11-wss", "SOAP 1.1 with WS-Security",
                "ASMX/WCF style service. The UsernameToken header is added automatically; success needs <Success>true</Success>.",
                () => new ProviderProfile
                {
                    Name = "SOAP 1.1 service",
                    Kind = "SOAP",
                    Method = "POST",
                    Url = "https://sms-service.local/SmsService.asmx",
                    ContentType = "text/xml; charset=utf-8",
                    Headers = { new ProfileHeader { Name = "SOAPAction", Value = "\"http://tempuri.org/SendSMS\"" } },
                    Body =
                        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                        "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:tem=\"http://tempuri.org/\">\n" +
                        "  <soapenv:Header/>\n" +
                        "  <soapenv:Body>\n" +
                        "    <tem:SendSMS>\n" +
                        "      <tem:Phone>{Phone}</tem:Phone>\n" +
                        "      <tem:Message>{Message}</tem:Message>\n" +
                        "      <tem:SendingSystem>{SendingSystem}</tem:SendingSystem>\n" +
                        "    </tem:SendSMS>\n" +
                        "  </soapenv:Body>\n" +
                        "</soapenv:Envelope>",
                    PhoneFormat = PhoneFormats.Local,
                    CountryCode = "972",
                    Variables =
                    {
                        new SmsParam { Key = "Username", Value = "" },
                        new SmsParam { Key = "Password", Value = "", IsSecret = true },
                        new SmsParam { Key = "SendingSystem", Value = "SCADA" },
                    },
                    Auth = new ProfileAuth { Type = AuthTypes.WsSecurity, User = "{Username}", Secret = "{Password}" },
                    Failure = new ResponseRule { StatusCodes = "", Match = MatchTypes.XmlElement, Expression = "ErrorMessage" },
                    Success = new ResponseRule { StatusCodes = "2xx", Match = MatchTypes.XmlElement, Expression = "Success", Expected = "true" },
                }),
            new ProfileStarter("soap12", "SOAP 1.2, no header auth",
                "SOAP 1.2 envelope (application/soap+xml). Credentials travel in the body.",
                () => new ProviderProfile
                {
                    Name = "SOAP 1.2 service",
                    Kind = "SOAP",
                    Method = "POST",
                    Url = "https://sms-service.local/sms.svc",
                    ContentType = "application/soap+xml; charset=utf-8; action=\"urn:SendMessage\"",
                    Body =
                        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                        "<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\">\n" +
                        "  <s:Body>\n" +
                        "    <SendMessage xmlns=\"urn:sms\">\n" +
                        "      <Account>{Account}</Account>\n" +
                        "      <Key>{ApiKey}</Key>\n" +
                        "      <Recipient>{Phone}</Recipient>\n" +
                        "      <Text>{Message}</Text>\n" +
                        "    </SendMessage>\n" +
                        "  </s:Body>\n" +
                        "</s:Envelope>",
                    PhoneFormat = PhoneFormats.E164,
                    CountryCode = "972",
                    Variables =
                    {
                        new SmsParam { Key = "Account", Value = "" },
                        new SmsParam { Key = "ApiKey", Value = "", IsSecret = true },
                    },
                    Success = new ResponseRule { StatusCodes = "2xx", Match = MatchTypes.XmlElement, Expression = "Status", Expected = "Accepted" },
                }),
        };
    }
}
