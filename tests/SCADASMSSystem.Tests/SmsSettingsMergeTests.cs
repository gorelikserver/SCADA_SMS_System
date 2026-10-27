using System.Text.Json;
using System.Text.Json.Nodes;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using Xunit;

namespace SCADASMSSystem.Tests;

public class SmsSettingsMergeTests
{
    private static JsonObject StoredSection() => JsonNode.Parse("""
        {
          "ProviderType": "REST",
          "ApiEndpoint": "https://old.example/send",
          "Username": "gateway-user",
          "Password": "s3cret",
          "ApiParams": "{\"to\":\"phone\"}",
          "RestBearerToken": "tok-123",
          "RateLimit": 10,
          "TestMode": true,
          "CustomVendorKey": "keep-me",
          "SoapPlaceholders": [
            { "Key": "Username", "Value": "soap-user", "IsSecret": false },
            { "Key": "Password", "Value": "soap-pass", "IsSecret": true }
          ]
        }
        """)!.AsObject();

    [Fact]
    public void ApplyPostedFields_KeepsFieldsThatWereNotPosted()
    {
        var section = StoredSection();
        var posted = new SmsSettings { ApiEndpoint = "https://new.example/send" };

        SmsSettingsMerge.ApplyPostedFields(section, posted, new[] { "ApiEndpoint" });

        Assert.Equal("https://new.example/send", (string?)section["ApiEndpoint"]);
        Assert.Equal("gateway-user", (string?)section["Username"]);
        Assert.Equal("s3cret", (string?)section["Password"]);
        Assert.Equal("{\"to\":\"phone\"}", (string?)section["ApiParams"]);
        Assert.Equal("tok-123", (string?)section["RestBearerToken"]);
        Assert.Equal("keep-me", (string?)section["CustomVendorKey"]);
    }

    [Fact]
    public void ApplyPostedFields_WritesPostedScalarsWithTheirTypes()
    {
        var section = StoredSection();
        var posted = new SmsSettings { RateLimit = 25, TestMode = false, ProviderType = "SOAP" };

        SmsSettingsMerge.ApplyPostedFields(section, posted, new[] { "RateLimit", "TestMode", "ProviderType" });

        Assert.Equal(25, (int)section["RateLimit"]!);
        Assert.False((bool)section["TestMode"]!);
        Assert.Equal("SOAP", (string?)section["ProviderType"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("***")]
    public void ApplyPostedFields_BlankOrMaskedSecret_KeepsStoredSecret(string postedValue)
    {
        var section = StoredSection();
        var posted = new SmsSettings { Password = postedValue, RestBearerToken = postedValue };

        SmsSettingsMerge.ApplyPostedFields(section, posted, new[] { "Password", "RestBearerToken" });

        Assert.Equal("s3cret", (string?)section["Password"]);
        Assert.Equal("tok-123", (string?)section["RestBearerToken"]);
    }

    [Fact]
    public void ApplyPostedFields_NewSecretValue_Overwrites()
    {
        var section = StoredSection();
        var posted = new SmsSettings { Password = "rotated" };

        SmsSettingsMerge.ApplyPostedFields(section, posted, new[] { "Password" });

        Assert.Equal("rotated", (string?)section["Password"]);
    }

    [Fact]
    public void ApplyPostedFields_IgnoresUnknownAndListFields()
    {
        var section = StoredSection();
        var before = section.ToJsonString();

        SmsSettingsMerge.ApplyPostedFields(section, new SmsSettings(), new[] { "NoSuchField", "SoapPlaceholders" });

        Assert.Equal(before, section.ToJsonString());
    }

    [Fact]
    public void ApplyParamList_NotPosted_KeepsStoredList()
    {
        var section = StoredSection();
        var before = section["SoapPlaceholders"]!.ToJsonString();

        SmsSettingsMerge.ApplyParamList(section, "SoapPlaceholders", null);

        Assert.Equal(before, section["SoapPlaceholders"]!.ToJsonString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("***")]
    public void ApplyParamList_BlankOrMaskedSecretRow_KeepsStoredValue(string postedValue)
    {
        var section = StoredSection();
        var posted = JsonSerializer.Serialize(new[]
        {
            new SmsParam { Key = "Username", Value = "new-user" },
            new SmsParam { Key = "Password", Value = postedValue, IsSecret = true },
        });

        SmsSettingsMerge.ApplyParamList(section, "SoapPlaceholders", posted);

        var list = section["SoapPlaceholders"]!.Deserialize<List<SmsParam>>()!;
        Assert.Equal("new-user", list.Single(p => p.Key == "Username").Value);
        Assert.Equal("soap-pass", list.Single(p => p.Key == "Password").Value);
    }

    [Fact]
    public void ApplyParamList_InvalidJson_ThrowsAndLeavesSectionUnchanged()
    {
        var section = StoredSection();
        var before = section.ToJsonString();

        Assert.ThrowsAny<JsonException>(() =>
            SmsSettingsMerge.ApplyParamList(section, "SoapPlaceholders", "{not json"));

        Assert.Equal(before, section.ToJsonString());
    }

    [Fact]
    public void ApplyParamList_ExplicitEmptyArray_ClearsList()
    {
        var section = StoredSection();

        SmsSettingsMerge.ApplyParamList(section, "SoapPlaceholders", "[]");

        Assert.Empty(section["SoapPlaceholders"]!.AsArray());
    }

    [Fact]
    public void MaskSecrets_HidesOnlySecretValuesAndRoundTripsThroughMerge()
    {
        var section = StoredSection();
        var stored = section["SoapPlaceholders"]!.Deserialize<List<SmsParam>>()!;

        var masked = SmsSettingsMerge.MaskSecrets(stored);

        Assert.Equal("soap-user", masked.Single(p => p.Key == "Username").Value);
        Assert.Equal("***", masked.Single(p => p.Key == "Password").Value);
        Assert.DoesNotContain("soap-pass", JsonSerializer.Serialize(masked));

        // What the browser received and posts back unchanged must keep the stored secret.
        SmsSettingsMerge.ApplyParamList(section, "SoapPlaceholders", JsonSerializer.Serialize(masked));
        Assert.Equal("soap-pass", section["SoapPlaceholders"]!.Deserialize<List<SmsParam>>()!.Single(p => p.Key == "Password").Value);
    }
}
