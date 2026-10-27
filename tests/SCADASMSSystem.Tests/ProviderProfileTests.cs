using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services.Providers;
using Xunit;

namespace SCADASMSSystem.Tests;

public class ProfileTemplateTests
{
    private static readonly ProfileContext Ctx = new("Pump \"A\" & <B> תקלה", "050-1234567", "85.3", "ALM-1", "urgent", 3, 7, "Dana", "Levi", "123");

    private static ProfileTemplate T(params SmsParam[] vars) =>
        new(new ProviderProfile { Variables = vars.ToList() }, Ctx, new DateTime(2026, 10, 27, 13, 5, 9));

    [Theory]
    [InlineData("{Message:raw}", "Pump \"A\" & <B> תקלה")]
    [InlineData("{Message:json}", "Pump \\\"A\\\" & <B> תקלה")]
    [InlineData("{Message:xml}", "Pump &quot;A&quot; &amp; &lt;B&gt; תקלה")]
    [InlineData("{Value:url}", "85.3")]
    [InlineData("{message:url}", "Pump%20%22A%22%20%26%20%3CB%3E%20%D7%AA%D7%A7%D7%9C%D7%94")]
    public void EncodingSuffixes(string template, string expected) =>
        Assert.Equal(expected, T().Render(template, "raw"));

    [Fact]
    public void BuiltInsAndVariables()
    {
        var rendered = T(new SmsParam { Key = "Sender", Value = "SCADA" })
            .Render("{AlarmId}|{Priority}|{GroupId}|{UserId}|{FirstName}|{LastName}|{TZ}|{Sender}|{Now:yyyyMMddHHmmss}", "raw");
        Assert.Equal("ALM-1|urgent|3|7|Dana|Levi|123|SCADA|20261027130509", rendered);
    }

    [Fact]
    public void DefaultEncodingAppliesWithoutSuffix() =>
        Assert.Equal("\"Pump \\\"A\\\" & <B> תקלה\"", T().Render("\"{Message}\"", Encodings.Json));

    [Fact]
    public void JsonBracesAreNotTokens() =>
        Assert.Equal("{\"to\":\"050-1234567\",\"x\":{ }}", T().Render("{\"to\":\"{Phone}\",\"x\":{ }}", Encodings.Json));

    [Fact]
    public void UnknownToken_Throws() =>
        Assert.Throws<TemplateException>(() => T().Render("<a>{Missing}</a>", "xml"));

    [Fact]
    public void UnknownEncoding_Throws() =>
        Assert.Throws<TemplateException>(() => T().Render("{Message:html}", "raw"));
}

public class PhoneFormatterTests
{
    [Theory]
    [InlineData("050-123 4567", "AsStored", "972", "050-123 4567")]
    [InlineData("050-123 4567", "DigitsOnly", "", "0501234567")]
    [InlineData("050-1234567", "E164", "972", "+972501234567")]
    [InlineData("+972 50 1234567", "E164", "972", "+972501234567")]
    [InlineData("00972501234567", "International", "972", "972501234567")]
    [InlineData("050-1234567", "International", "972", "972501234567")]
    [InlineData("+972-50-1234567", "Local", "972", "0501234567")]
    [InlineData("050-1234567", "Local", "972", "0501234567")]
    public void Formats(string stored, string format, string cc, string expected) =>
        Assert.Equal(expected, PhoneFormatter.Format(stored, format, cc));
}

public class ResponseEvaluatorTests
{
    private const string SoapSuccess = """
        <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/">
          <SOAP-ENV:Body>
            <SendSMSResponse xmlns="http://tempuri.org">
              <SendSMSResult><Success>true</Success><ErrorMessage/></SendSMSResult>
            </SendSMSResponse>
          </SOAP-ENV:Body>
        </SOAP-ENV:Envelope>
        """;

    private static ProviderProfile Soap() => new()
    {
        Kind = "SOAP",
        Success = new ResponseRule { StatusCodes = "2xx", Match = MatchTypes.XmlElement, Expression = "Success", Expected = "true" },
        Failure = new ResponseRule { StatusCodes = "", Match = MatchTypes.XmlElement, Expression = "ErrorMessage" },
    };

    [Fact]
    public void SoapSuccess_IsAccepted() => Assert.True(ResponseEvaluator.Evaluate(Soap(), 200, SoapSuccess).Accepted);

    [Fact]
    public void FilledErrorMessage_OverridesSuccess()
    {
        var verdict = ResponseEvaluator.Evaluate(Soap(), 200, SoapSuccess.Replace("<ErrorMessage/>", "<ErrorMessage>Invalid phone</ErrorMessage>"));
        Assert.False(verdict.Accepted);
        Assert.Contains("Invalid phone", verdict.Reason);
    }

    [Fact]
    public void SoapFault_IsRejectedEvenOn200()
    {
        const string fault = """<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultstring>Provider unavailable</faultstring></soap:Fault></soap:Body></soap:Envelope>""";
        var verdict = ResponseEvaluator.Evaluate(Soap(), 200, fault);
        Assert.False(verdict.Accepted);
        Assert.Contains("Provider unavailable", verdict.Reason);
    }

    [Fact]
    public void MalformedXml_IsNotAccepted() => Assert.False(ResponseEvaluator.Evaluate(Soap(), 200, "<Envelope><broken>").Accepted);

    [Fact]
    public void TwoSuccessElements_AreAmbiguous() =>
        Assert.False(ResponseEvaluator.Evaluate(Soap(), 200, SoapSuccess.Replace("<ErrorMessage/>", "<Success>true</Success>")).Accepted);

    [Fact]
    public void SuccessFalse_IsNotAccepted() =>
        Assert.False(ResponseEvaluator.Evaluate(Soap(), 200, SoapSuccess.Replace(">true<", ">false<")).Accepted);

    [Theory]
    [InlineData("2xx", 204, true)]
    [InlineData("2xx", 302, false)]
    [InlineData("200,202", 202, true)]
    [InlineData("200,202", 201, false)]
    [InlineData("", 500, true)]
    public void StatusRules(string rule, int code, bool expected) => Assert.Equal(expected, ResponseEvaluator.StatusMatches(rule, code));

    [Theory]
    [InlineData("""{"status":"OK","id":"m1"}""", "$.status", "ok", true)]
    [InlineData("""{"status":"ERROR"}""", "$.status", "ok", false)]
    [InlineData("""{"results":[{"accepted":true}]}""", "$.results[0].accepted", "true", true)]
    [InlineData("""{"results":[]}""", "$.results[0].accepted", "true", false)]
    [InlineData("""{"messageId":"abc"}""", "$.messageId", "", true)]
    [InlineData("not json", "$.status", "ok", false)]
    public void JsonPathSuccess(string body, string path, string expected, bool accepted)
    {
        var profile = new ProviderProfile { Success = new ResponseRule { Match = MatchTypes.JsonPath, Expression = path, Expected = expected } };
        Assert.Equal(accepted, ResponseEvaluator.Evaluate(profile, 200, body).Accepted);
    }

    [Fact]
    public void FailureRuleWinsOver2xx()
    {
        var profile = new ProviderProfile { Failure = new ResponseRule { StatusCodes = "", Match = MatchTypes.Contains, Expression = "ERR" } };
        Assert.False(ResponseEvaluator.Evaluate(profile, 200, "ERR-17 invalid number").Accepted);
        Assert.True(ResponseEvaluator.Evaluate(profile, 200, "OK").Accepted);
    }

    [Fact]
    public void RegexSuccess()
    {
        var profile = new ProviderProfile { Success = new ResponseRule { Match = MatchTypes.Regex, Expression = @"^\d+$" } };
        Assert.True(ResponseEvaluator.Evaluate(profile, 200, "48213").Accepted);
        Assert.False(ResponseEvaluator.Evaluate(profile, 200, "-1").Accepted);
    }
}

public class ProviderRequestBuilderTests
{
    private static readonly ProfileContext Ctx = new("Pump & fail", "050-1234567", AlarmId: "ALM-1");

    private static string BodyOf(BuiltRequest r) => r.Request.Content == null ? "" : r.Request.Content.ReadAsStringAsync().Result;

    [Fact]
    public void Get_PutsEncodedValuesInQuery()
    {
        var profile = new ProviderProfile
        {
            Method = "GET",
            Url = "https://gw.example/send?to={Phone}&msg={Message}&user={User}",
            ContentType = "",
            Variables = { new SmsParam { Key = "User", Value = "a b" } },
        };
        using var r = ProviderRequestBuilder.Build(profile, Ctx);
        Assert.Equal("https://gw.example/send?to=050-1234567&msg=Pump%20%26%20fail&user=a%20b", r.Request.RequestUri!.AbsoluteUri);
        Assert.Null(r.Request.Content);
    }

    [Fact]
    public void JsonPost_WithBearerAndSecretHeader_MasksDisplay()
    {
        var profile = new ProviderProfile
        {
            Url = "https://api.example/v1/sms",
            Body = """{"to":"{Phone}","text":"{Message}","ref":"{AlarmId}"}""",
            PhoneFormat = PhoneFormats.E164,
            CountryCode = "972",
            Headers = { new ProfileHeader { Name = "X-Api-Key", Value = "{Key}" } },
            Variables = { new SmsParam { Key = "Key", Value = "k-123", IsSecret = true }, new SmsParam { Key = "Token", Value = "t-999", IsSecret = true } },
            Auth = new ProfileAuth { Type = AuthTypes.Bearer, Secret = "{Token}" },
        };
        using var r = ProviderRequestBuilder.Build(profile, Ctx);

        var json = JsonDocument.Parse(BodyOf(r)).RootElement;
        Assert.Equal("+972501234567", json.GetProperty("to").GetString());
        Assert.Equal("Pump & fail", json.GetProperty("text").GetString());
        Assert.Equal("Bearer t-999", r.Request.Headers.Authorization!.ToString());
        Assert.Equal("k-123", r.Request.Headers.GetValues("X-Api-Key").Single());
        Assert.Equal("application/json; charset=utf-8", r.Request.Content!.Headers.ContentType!.ToString());
        Assert.DoesNotContain("k-123", r.DisplayBody + r.DisplayUrl);
    }

    [Fact]
    public void Form_Post_And_Basic()
    {
        var profile = new ProviderProfile
        {
            Url = "https://gw.example/send",
            ContentType = "application/x-www-form-urlencoded",
            Body = "to={Phone}&text={Message}",
            Variables = { new SmsParam { Key = "U", Value = "user" }, new SmsParam { Key = "P", Value = "pa:ss", IsSecret = true } },
            Auth = new ProfileAuth { Type = AuthTypes.Basic, User = "{U}", Secret = "{P}" },
        };
        using var r = ProviderRequestBuilder.Build(profile, Ctx);
        Assert.Equal("to=050-1234567&text=Pump%20%26%20fail", BodyOf(r));
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pa:ss")), r.Request.Headers.Authorization!.ToString());
    }

    [Fact]
    public void Soap12_WsSecurity_IsInjectedIntoHeader_AndMaskedInDisplay()
    {
        var profile = new ProviderProfile
        {
            Kind = "SOAP",
            Url = "https://soap.example/sms.asmx",
            ContentType = "application/soap+xml; charset=utf-8",
            Body = """<s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"><s:Body><Send xmlns="urn:x"><Phone>{Phone}</Phone><Text>{Message}</Text></Send></s:Body></s:Envelope>""",
            Variables = { new SmsParam { Key = "U", Value = "svc" }, new SmsParam { Key = "P", Value = "S3cret&<", IsSecret = true } },
            Auth = new ProfileAuth { Type = AuthTypes.WsSecurity, User = "{U}", Secret = "{P}" },
        };
        using var r = ProviderRequestBuilder.Build(profile, Ctx);

        var doc = XDocument.Parse(BodyOf(r));
        XNamespace s = "http://www.w3.org/2003/05/soap-envelope";
        var header = doc.Root!.Element(s + "Header");
        Assert.NotNull(header);
        Assert.Equal("S3cret&<", header!.Descendants().Single(e => e.Name.LocalName == "Password").Value);
        Assert.Equal("Pump & fail", doc.Descendants().Single(e => e.Name.LocalName == "Text").Value);
        Assert.DoesNotContain("S3cret", r.DisplayBody);
    }
}

public class LegacyProfileConverterTests
{
    private static string BodyOf(BuiltRequest r) => r.Request.Content == null ? "" : r.Request.Content.ReadAsStringAsync().Result;

    [Fact]
    public void LegacyGetWithApiParams_ProducesSameUrlAsOldCode()
    {
        // Shape of appsettings.Development.json in the repo.
        var legacy = new LegacySmsSettings
        {
            ProviderType = "REST",
            ApiEndpoint = "http://localhost:5555/services/SendMessage.asmx/SendMessagesReturenMessageID",
            HttpMethod = "GET",
            ApiParams = """{"SendToPhoneNumbers":"phone","Message":"message","UserName":"username","Password":"password","SenderName":"sender_name"}""",
            Username = "mock_user",
            Password = "p@ss word",
            SenderName = "",
        };
        var profile = LegacyProfileConverter.Convert(legacy);
        Assert.Empty(ProfileValidator.Validate(profile));

        using var r = ProviderRequestBuilder.Build(profile, new ProfileContext("Hi & bye", "+1-555-0001"));
        // Old code: endpoint?k=EscapeDataString(v)&..., sender defaulting to SCADA.
        Assert.Equal(
            "http://localhost:5555/services/SendMessage.asmx/SendMessagesReturenMessageID" +
            "?SendToPhoneNumbers=%2B1-555-0001&Message=Hi%20%26%20bye&UserName=mock_user&Password=p%40ss%20word&SenderName=SCADA",
            r.Request.RequestUri!.AbsoluteUri);
        Assert.True(profile.Variables.Single(v => v.Key == "Password").IsSecret);
    }

    [Fact]
    public void RestJsonWithBodyParamsHeadersAndPatterns()
    {
        var legacy = new LegacySmsSettings
        {
            ProviderType = "REST",
            ApiEndpoint = "https://api.example/send",
            HttpMethod = "POST",
            ContentType = "application/json",
            RestBodyParams = { new SmsParam { Key = "to", Value = "phone" }, new SmsParam { Key = "text", Value = "message" }, new SmsParam { Key = "account", Value = "ACC-{7}" } },
            RestHeaders = { new SmsParam { Key = "X-Token", Value = "tok", IsSecret = true } },
            RestSuccessPattern = "\"ok\":true",
            RestFailurePattern = "error",
        };
        var profile = LegacyProfileConverter.Convert(legacy);
        Assert.Empty(ProfileValidator.Validate(profile));

        using var r = ProviderRequestBuilder.Build(profile, new ProfileContext("A \"b\"", "0501234567"));
        var json = JsonDocument.Parse(BodyOf(r)).RootElement;
        Assert.Equal("0501234567", json.GetProperty("to").GetString());
        Assert.Equal("A \"b\"", json.GetProperty("text").GetString());
        Assert.Equal("ACC-{7}", json.GetProperty("account").GetString());
        Assert.Equal("tok", r.Request.Headers.GetValues("X-Token").Single());
        Assert.False(ResponseEvaluator.Evaluate(profile, 200, """{"ok":true,"error":"x"}""").Accepted);
        Assert.True(ResponseEvaluator.Evaluate(profile, 200, """{"ok":true}""").Accepted);
    }

    [Fact]
    public void ApiKeyInQuery()
    {
        var legacy = new LegacySmsSettings
        {
            ApiEndpoint = "https://gw.example/send",
            HttpMethod = "GET",
            ApiParams = """{"to":"phone","msg":"message"}""",
            RestAuthType = "ApiKey",
            RestApiKeyName = "api_key",
            RestApiKeyValue = "k/1",
            RestApiKeyLocation = "Query",
        };
        var profile = LegacyProfileConverter.Convert(legacy);
        using var r = ProviderRequestBuilder.Build(profile, new ProfileContext("x", "1234567"));
        Assert.EndsWith("&api_key=k%2F1", r.Request.RequestUri!.AbsoluteUri);
        Assert.DoesNotContain("k%2F1", r.DisplayUrl);
    }

    [Fact]
    public void CustomerSoapTemplate_WithPlaceholdersAndWsSecurity()
    {
        var legacy = new LegacySmsSettings
        {
            ProviderType = "SOAP",
            ApiEndpoint = "https://soap.example/sms.asmx",
            SoapAction = "http://tempuri.org/SendSMS",
            SoapAuthType = "WSSecurity",
            SoapEnvelopeNamespaces = "xmlns:tem=\"http://tempuri.org\"",
            SoapBodyTemplate = """
                <tem:SendSMS>
                  <tem:SendingSystem>{SendingSystem}</tem:SendingSystem>
                  <tem:MessageType>{MessageType}</tem:MessageType>
                  <tem:Message>{Msg}</tem:Message>
                  <tem:TZ>{TZ}</tem:TZ>
                  <tem:Phone>{Recipient}</tem:Phone>
                </tem:SendSMS>
                """,
            SoapPlaceholders =
            {
                new SmsParam { Key = "Recipient", Value = "phone" },
                new SmsParam { Key = "Msg", Value = "message" },
                new SmsParam { Key = "SendingSystem", Value = "sendingsystem" },
                new SmsParam { Key = "MessageType", Value = "messagetype" },
                new SmsParam { Key = "TZ", Value = "tz" },
            },
            Username = "svc",
            Password = "pw",
            SoapSendingSystem = "SCADA-BY",
            SoapSuccessPattern = "<Success xmlns=\"http://tempuri.org\">true</Success>",
            SoapErrorPattern = "<ErrorMessage>",
        };
        var profile = LegacyProfileConverter.Convert(legacy);
        Assert.Empty(ProfileValidator.Validate(profile));
        Assert.Equal(MatchTypes.XmlElement, profile.Success.Match);

        using var r = ProviderRequestBuilder.Build(profile, new ProfileContext("Fire & smoke", "0501234567", TZ: "123456789"));
        var doc = XDocument.Parse(BodyOf(r));
        string Field(string name) => doc.Descendants().Single(e => e.Name.LocalName == name).Value;
        Assert.Equal("Fire & smoke", Field("Message"));
        Assert.Equal("0501234567", Field("Phone"));
        Assert.Equal("SCADA-BY", Field("SendingSystem"));
        Assert.Equal("SmsType1", Field("MessageType"));
        Assert.Equal("123456789", Field("TZ"));
        Assert.Equal("pw", Field("Password"));
        Assert.Equal("\"http://tempuri.org/SendSMS\"", r.Request.Headers.GetValues("SOAPAction").Single());
        Assert.Equal("text/xml; charset=utf-8", r.Request.Content!.Headers.ContentType!.ToString());
    }

    [Fact]
    public void UnmappedSoapToken_IsReportedByValidator()
    {
        var legacy = new LegacySmsSettings
        {
            ProviderType = "SOAP",
            ApiEndpoint = "https://soap.example/sms.asmx",
            SoapAuthType = "None",
            SoapBodyTemplate = "<Send><P>{Phone}</P><M>{Message}</M><X>{Unmapped}</X></Send>",
            SoapPlaceholders = { new SmsParam { Key = "Phone", Value = "phone" }, new SmsParam { Key = "Message", Value = "message" } },
            SoapSuccessPattern = "<Success>true</Success>",
        };
        Assert.Contains(ProfileValidator.Validate(LegacyProfileConverter.Convert(legacy)), e => e.Contains("{Unmapped}"));
    }
}

public class ProfileValidatorTests
{
    private static ProviderProfile Valid() => new()
    {
        Url = "https://api.example/send",
        Body = """{"to":"{Phone}","text":"{Message}"}""",
    };

    [Fact]
    public void ValidProfile_HasNoErrors() => Assert.Empty(ProfileValidator.Validate(Valid()));

    [Fact]
    public void MissingPhone_IsAnError()
    {
        var p = Valid();
        p.Body = """{"text":"{Message}"}""";
        Assert.Contains(ProfileValidator.Validate(p), e => e.Contains("{Phone}"));
    }

    [Fact]
    public void JsonBodyThatBreaksWithRawValues_IsAnError()
    {
        var p = Valid();
        p.Body = """{"to":"{Phone}","text":"{Message:raw}"}""";
        Assert.Contains(ProfileValidator.Validate(p), e => e.Contains("not valid JSON"));
    }

    [Fact]
    public void SoapWithoutSuccessRule_IsAnError()
    {
        var p = Valid();
        p.Kind = "SOAP";
        p.ContentType = "text/xml";
        p.Body = "<e:Envelope xmlns:e=\"http://schemas.xmlsoap.org/soap/envelope/\"><e:Body><S><P>{Phone}</P><M>{Message}</M></S></e:Body></e:Envelope>";
        Assert.Contains(ProfileValidator.Validate(p), e => e.Contains("success rule"));
    }

    [Fact]
    public void ReservedOrDuplicateVariables_AreErrors()
    {
        var p = Valid();
        p.Variables.Add(new SmsParam { Key = "Phone", Value = "x" });
        p.Variables.Add(new SmsParam { Key = "A", Value = "1" });
        p.Variables.Add(new SmsParam { Key = "a", Value = "2" });
        var errors = ProfileValidator.Validate(p);
        Assert.Contains(errors, e => e.Contains("reserved"));
        Assert.Contains(errors, e => e.Contains("defined twice"));
    }

    [Fact]
    public void NonHttpUrl_IsAnError()
    {
        var p = Valid();
        p.Url = "ftp://x/{Phone}";
        Assert.NotEmpty(ProfileValidator.Validate(p));
    }

    [Fact]
    public void NullProfile_IsAnError() => Assert.NotEmpty(ProfileValidator.Validate(null));
}
