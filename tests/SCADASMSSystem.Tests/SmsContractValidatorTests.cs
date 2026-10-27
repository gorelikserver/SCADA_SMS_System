using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using Xunit;

namespace SCADASMSSystem.Tests;

public class SmsContractValidatorTests
{
    [Fact]
    public void Rest_requires_exactly_one_phone_and_message_mapping()
    {
        var settings = new SmsSettings
        {
            ProviderType = "REST",
            ApiEndpoint = "https://sms.example.test/send",
            HttpMethod = "POST",
            RestBodyParams = [
                new() { Key = "to", Value = "phone" },
                new() { Key = "text", Value = "message" }
            ]
        };

        Assert.Empty(SmsContractValidator.Validate(settings));
        settings.RestBodyParams.Add(new SmsParam { Key = "mobile", Value = "mobile" });
        Assert.Contains(SmsContractValidator.Validate(settings), e => e.Contains("exactly one parameter to 'phone'"));
    }

    [Fact]
    public void Soap_accepts_configurable_namespaced_contract()
    {
        var settings = new SmsSettings
        {
            ProviderType = "SOAP",
            ApiEndpoint = "https://sms.example.test/service.svc",
            SoapAction = "urn:SendSMS",
            SoapEnvelopeNamespaces = "xmlns:tem=\"urn:example\"",
            SoapBodyTemplate = "<tem:SendSMS><tem:Target>{Recipient}</tem:Target><tem:Text>{Payload}</tem:Text></tem:SendSMS>",
            SoapSuccessPattern = "<Success xmlns=\"urn:example\">true</Success>",
            SoapPlaceholders = [
                new() { Key = "Recipient", Value = "phone" },
                new() { Key = "Payload", Value = "message" }
            ]
        };

        Assert.Empty(SmsContractValidator.Validate(settings));
    }

    [Fact]
    public void Soap_rejects_missing_or_duplicate_delivery_tokens()
    {
        var settings = new SmsSettings
        {
            ProviderType = "SOAP",
            ApiEndpoint = "https://sms.example.test/service.svc",
            SoapAction = "urn:SendSMS",
            SoapBodyTemplate = "<Send><To>{Recipient}</To><Text>{Payload}</Text><Copy>{Payload}</Copy></Send>",
            SoapSuccessPattern = "<Success>true</Success>",
            SoapPlaceholders = [
                new() { Key = "Recipient", Value = "phone" },
                new() { Key = "Payload", Value = "message" }
            ]
        };

        Assert.Contains(SmsContractValidator.Validate(settings), e => e.Contains("exactly one {Payload}"));
    }
}
