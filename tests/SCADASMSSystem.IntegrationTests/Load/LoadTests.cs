using SCADASMSSystem.IntegrationTests.Infrastructure;
using SCADASMSSystem.IntegrationTests.Mock;
using Xunit;

namespace SCADASMSSystem.IntegrationTests.Load;

[Trait("Category", "Load")]
public class RestLoadTests : LoadTests
{
    protected override string Kind => "REST";
}

[Trait("Category", "Load")]
public class SoapLoadTests : LoadTests
{
    protected override string Kind => "SOAP";

    /// <summary>
    /// The shipped defaults (RateLimit 10 per 60 s, 30 s timeout, 3 retries, bat MAXTIME 120 s, spool
    /// MinAge 150 s) under a 10-alarm storm: shows what the global rate limit does to delivery time.
    /// </summary>
    [MockItFact]
    public Task L7_burst_10_with_shipped_default_limits() => Run(new LoadSpec("L7-defaults-burst10", Kind,
        "10 concurrent alarms, shipped default limits (RateLimit 10/60 s, MAXTIME 120 s)", 10,
        Options: new SiteOptions
        {
            RateLimit = 10, RateWindow = 60, TimeoutSeconds = 30, RetryAttempts = 3,
            BatMaxTimeSeconds = 120, SpoolMinAgeSeconds = 150, SpoolPollSeconds = 30,
        },
        ConvergeTimeout: TimeSpan.FromMinutes(9)));
}

/// <summary>
/// Alarm-storm load tests. Each run uses a fresh site; zero-loss (every alarm x recipient eventually
/// accepted, spool empty, DB all SUCCESS) is the pass criterion. Timing numbers go to the report.
/// </summary>
public abstract class LoadTests
{
    protected abstract string Kind { get; }

    protected static async Task Run(LoadSpec spec)
    {
        var result = await LoadRunner.RunAsync(spec);
        Assert.True(result.ZeroLoss, result.Failure);
        // DEGRADED (duplicates) passes here and is flagged by the report; INDETERMINATE fails.
        Assert.True(result.Verdict is "PASS" or "DEGRADED", $"{result.Verdict}: {result.Failure}");
    }

    [MockItFact]
    public Task L1_burst_10() => Run(new LoadSpec("L1-burst10", Kind, "10 concurrent alarms, healthy provider", 10));

    [MockItFact]
    public Task L2_burst_50() => Run(new LoadSpec("L2-burst50", Kind, "50 concurrent alarms, healthy provider", 50));

    [MockItFact]
    public Task L3_burst_200() => Run(new LoadSpec("L3-burst200", Kind, "200 concurrent alarms, healthy provider", 200,
        ConvergeTimeout: TimeSpan.FromMinutes(6)));

    [MockItFact]
    public Task L4_burst_50_latency_300ms() => Run(new LoadSpec("L4-burst50-latency", Kind, "50 concurrent alarms, provider latency 300 ms per SMS", 50,
        ProviderLatencyMs: 300));

    [MockItFact]
    public Task L5_burst_50_http5xx_10pct() => Run(new LoadSpec("L5-burst50-5xx", Kind, "50 concurrent alarms, 10% of provider calls HTTP 503", 50,
        Faults: [new FaultRule { Kind = FaultKind.Http5xx, StatusCode = 503, Probability = 0.10 }]));

    [MockItFact]
    public Task L6_burst_50_timeouts_5pct() => Run(new LoadSpec("L6-burst50-timeouts", Kind, "50 concurrent alarms, 5% of provider calls hang until timeout (3 s)", 50,
        Faults: [new FaultRule { Kind = FaultKind.Hang, Probability = 0.05 }],
        ConvergeTimeout: TimeSpan.FromMinutes(5)));
}
