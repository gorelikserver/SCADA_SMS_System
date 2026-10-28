using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;
using Xunit;

namespace SCADASMSSystem.Tests;

public class CriticalDeliveryPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 12, 0, 0);
    private static readonly Func<CriticalSmsDelivery, bool> Claimable =
        CriticalDeliveryPolicy.Claimable(CriticalDeliveryPolicy.StaleBefore(Now)).Compile();

    private static CriticalSmsDelivery Row(string status, TimeSpan age) =>
        new() { Status = status, UpdatedAt = Now - age };

    [Theory]
    [InlineData(CriticalDeliveryStatus.Failed)]
    [InlineData(CriticalDeliveryStatus.Indeterminate)]
    public void FailedAndIndeterminate_AreClaimable(string status) =>
        Assert.True(Claimable(Row(status, TimeSpan.FromSeconds(1))));

    [Fact]
    public void Success_IsNeverClaimable() =>
        Assert.False(Claimable(Row(CriticalDeliveryStatus.Success, TimeSpan.FromDays(1))));

    [Fact]
    public void FreshSending_IsNotClaimable() =>
        Assert.False(Claimable(Row(CriticalDeliveryStatus.Sending, TimeSpan.FromMinutes(1))));

    [Fact]
    public void StaleSending_IsClaimable() =>
        Assert.True(Claimable(Row(CriticalDeliveryStatus.Sending, CriticalDeliveryPolicy.StaleSendingAfter + TimeSpan.FromSeconds(1))));

    [Theory]
    [InlineData(0, true)]     // timeout or transport loss
    [InlineData(429, true)]   // throttled
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(400, false)]  // permanent rejection
    [InlineData(401, false)]
    [InlineData(200, false)]  // 2xx with failure pattern is a provider decision
    public void IsRetryable_ByStatusCode(int statusCode, bool expected) =>
        Assert.Equal(expected, CriticalDeliveryPolicy.IsRetryable(new SmsApiResponse { Success = false, StatusCode = statusCode }));

    [Fact]
    public void Success_IsNotRetryable() =>
        Assert.False(CriticalDeliveryPolicy.IsRetryable(new SmsApiResponse { Success = true, StatusCode = 0 }));
}
