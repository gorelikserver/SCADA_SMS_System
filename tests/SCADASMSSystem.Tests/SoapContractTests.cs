using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using Xunit;

namespace SCADASMSSystem.Tests;

public class SoapContractTests
{
    private const string SuccessResponse = """
        <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/">
          <SOAP-ENV:Body>
            <SendSMSResponse xmlns="http://tempuri.org">
              <SendSMSResult>
                <Success>true</Success>
                <ErrorMessage/>
              </SendSMSResult>
            </SendSMSResponse>
          </SOAP-ENV:Body>
        </SOAP-ENV:Envelope>
        """;

    [Fact]
    public void CustomerSuccessResponse_IsAccepted()
    {
        var result = SmsService.EvaluateSoapResponse(
            SuccessResponse,
            "<Success xmlns=\"http://tempuri.org\">true</Success>",
            "<ErrorMessage>");

        Assert.True(result.Success);
        Assert.Empty(result.ErrorMessage);
    }

    [Fact]
    public void PopulatedErrorMessage_OverridesSuccess()
    {
        var response = SuccessResponse.Replace("<ErrorMessage/>", "<ErrorMessage>Invalid phone</ErrorMessage>");

        var result = SmsService.EvaluateSoapResponse(
            response,
            "<Success xmlns=\"http://tempuri.org\">true</Success>",
            "<ErrorMessage>");

        Assert.False(result.Success);
        Assert.Equal("Invalid phone", result.ErrorMessage);
    }

    [Fact]
    public void SoapFault_IsRejected()
    {
        const string response = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <soap:Fault><faultcode>soap:Server</faultcode><faultstring>Provider unavailable</faultstring></soap:Fault>
              </soap:Body>
            </soap:Envelope>
            """;

        var result = SmsService.EvaluateSoapResponse(
            response,
            "<Success xmlns=\"http://tempuri.org\">true</Success>",
            "<ErrorMessage>");

        Assert.False(result.Success);
        Assert.Equal("Provider unavailable", result.ErrorMessage);
    }

    [Fact]
    public void MalformedXml_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => SmsService.EvaluateSoapResponse(
            "<Envelope><broken>",
            "<Success>true</Success>",
            "<ErrorMessage>"));
    }

    [Fact]
    public void MissingSuccessPattern_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => SmsService.EvaluateSoapResponse(
            SuccessResponse,
            "",
            "<ErrorMessage>"));
    }

    [Fact]
    public void CustomerTemplate_RequiresPhoneAndMessageExactlyOnce()
    {
        const string template = """
            <tem:SendSMS xmlns:tem="http://tempuri.org">
              <tem:SendingSystem>{SendingSystem}</tem:SendingSystem>
              <tem:MessageType>{MessageType}</tem:MessageType>
              <tem:Message>{Message}</tem:Message>
              <tem:FirstName>{FirstName}</tem:FirstName>
              <tem:LastName>{LastName}</tem:LastName>
              <tem:TZ>{TZ}</tem:TZ>
              <tem:Phone>{Phone}</tem:Phone>
            </tem:SendSMS>
            """;
        var mappings = new List<SmsParam>
        {
            new() { Key = "Phone", Value = "phone" },
            new() { Key = "Message", Value = "message" }
        };

        SmsService.ValidateRequiredSoapMapping(template, mappings,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "phone" }, "phone");
        SmsService.ValidateRequiredSoapMapping(template, mappings,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "message" }, "message");
    }

    [Fact]
    public void MappingWithoutTemplateToken_IsRejected()
    {
        var mappings = new List<SmsParam> { new() { Key = "Recipient", Value = "phone" } };

        Assert.Throws<InvalidOperationException>(() => SmsService.ValidateRequiredSoapMapping(
            "<Phone>{Phone}</Phone>", mappings,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "phone" }, "phone"));
    }
}
