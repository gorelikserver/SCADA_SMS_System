using SCADASMSSystem.Web.Models;
using Xunit;

namespace SCADASMSSystem.Tests;

public class PhoneNumberRulesTests
{
    [Theory]
    [InlineData("+972-50-1234567")]
    [InlineData("050 123 4567")]
    [InlineData("(03) 555.1234")]
    [InlineData("+1-555-0001")]
    [InlineData("1234567")]
    [InlineData("+123456789012345")]
    public void Valid(string phone) => Assert.True(PhoneNumberRules.IsValid(phone));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("+---")]
    [InlineData("123456")]            // too few digits
    [InlineData("1234567890123456")]  // more than E.164 allows
    [InlineData("050-123-4567 ext 2")]
    [InlineData("05O-1234567")]       // letter O
    [InlineData("97250+1234567")]     // + not at start
    public void Invalid(string? phone) => Assert.False(PhoneNumberRules.IsValid(phone));

    [Fact]
    public void SamePhone_IgnoresSeparators() =>
        Assert.True(PhoneNumberRules.SamePhone("+972-50-123 4567", "972501234567"));

    [Fact]
    public void SamePhone_EmptyNeverMatches() =>
        Assert.False(PhoneNumberRules.SamePhone("", "---"));

    [Fact]
    public void Attribute_AllowsEmptySoRequiredReportsIt() =>
        Assert.True(new SmsPhoneNumberAttribute().IsValid(""));
}
