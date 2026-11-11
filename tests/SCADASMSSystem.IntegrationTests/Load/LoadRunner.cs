using System.Text.RegularExpressions;
using SCADASMSSystem.IntegrationTests.Infrastructure;
using SCADASMSSystem.IntegrationTests.Mock;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.IntegrationTests.Load;

public sealed record LoadSpec(
    string Id,
    string Kind,
    string Title,
    int Burst,
    int Groups = 5,
    int RecipientsPerGroup = 3,
    int ProviderLatencyMs = 0,
    FaultRule[]? Faults = null,
    SiteOptions? Options = null,
    TimeSpan? ConvergeTimeout = null);

public sealed record Percentiles(double P50, double P95, double P99, double Max, double Mean, double Min);

/// <summary>Everything one alarm-storm run measured; serialized for the report.</summary>
public sealed class LoadResult
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public int Burst { get; init; }
    public int Groups { get; init; }
    public int RecipientsPerGroup { get; init; }
    public int ProviderLatencyMs { get; init; }
    public string[] Faults { get; init; } = [];
    public object? Config { get; set; }
    public string Status { get; set; } = "running";
    public string? Failure { get; set; }

    public double BurstWallSeconds { get; set; }
    public double AlarmsPerSecond { get; set; }
    public double SmsPerSecondDuringBurst { get; set; }
    public double SecondsToFullDelivery { get; set; }
    public Percentiles? BatLatencyMs { get; set; }
    public Percentiles? BatLatencyExit0Ms { get; set; }
    public Dictionary<string, int> ExitCodes { get; set; } = new();
    public double[] BatLatenciesMs { get; set; } = [];
    public double[] BatStartOffsetsMs { get; set; } = [];
    public int[] BatExitCodes { get; set; } = [];

    public int ExpectedSms { get; set; }
    public int ProviderRequests { get; set; }
    public int ProviderAccepted { get; set; }
    public int ProviderRefused { get; set; }
    public int ProviderNoAnswer { get; set; }
    public int ProviderContractErrors { get; set; }
    public int MaxProviderConcurrency { get; set; }
    public Percentiles? ProviderServiceMs { get; set; }
    public int UniquePairsAccepted { get; set; }
    public int MissingPairs { get; set; }
    public int DuplicateAcceptedSms { get; set; }
    public int DbRows { get; set; }
    public int DbRowsSuccess { get; set; }
    public Dictionary<string, int> DbStatus { get; set; } = new();
    public int DbDistinctAlarms { get; set; }
    public int SpoolResidual { get; set; }
    public int SpoolFailed { get; set; }
    public int SpoolBad { get; set; }
    public int BatLogLines { get; set; }
    public bool ZeroLoss { get; set; }
    public bool NoDuplicates { get; set; }
    /// <summary>PASS (zero loss, no duplicates), DEGRADED (zero loss, duplicates), FAIL (loss or harness failure).</summary>
    public string Verdict { get; set; } = "FAIL";
    public int HarnessTimeouts { get; set; }
    public double SettleSeconds { get; set; }
    public List<string> MissingSample { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

/// <summary>
/// Alarm storm: N concurrent Scada_sms.bat processes (Akara's 3 arguments) against groups of several
/// recipients, then reconciliation of mock records vs DB vs spool until every alarm x recipient was
/// accepted (zero-loss) or the timeout passes.
/// </summary>
public static class LoadRunner
{
    private static readonly Regex TokenRx = new(@"LOAD-[A-Za-z0-9]+-\d{4}");

    public static async Task<LoadResult> RunAsync(LoadSpec spec)
    {
        var result = new LoadResult
        {
            Id = spec.Id, Kind = spec.Kind, Title = spec.Title, Burst = spec.Burst, Groups = spec.Groups,
            RecipientsPerGroup = spec.RecipientsPerGroup, ProviderLatencyMs = spec.ProviderLatencyMs,
            Faults = spec.Faults?.Select(f => f.ToString()).ToArray() ?? [],
        };
        await using var site = await Site.CreateAsync(spec.Kind, spec.Id, spec.Options);
        try
        {
            result.Config = site.DescribeConfig();
            await site.StartAppAsync();

            // Warm-up alarm (JIT, connection pools), excluded from the numbers.
            var warm = await TestDb.CreateGroupAsync($"{spec.Kind}-{spec.Id}-warm", 1);
            await site.RunBatAsync("warm-up", warm.GroupId.ToString(), "0");
            var warmLogLines = site.BatLogLines().Length;
            site.Mock.ClearRequests();

            var groups = new List<SeededGroup>();
            for (var g = 0; g < spec.Groups; g++)
                groups.Add(await TestDb.CreateGroupAsync($"{spec.Kind}-{spec.Id}-g{g + 1}", spec.RecipientsPerGroup));

            site.Mock.BaseLatencyMs = spec.ProviderLatencyMs;
            foreach (var f in spec.Faults ?? []) site.Mock.AddFault(f);

            var runTag = spec.Id.Replace("-", "");
            var alarms = Enumerable.Range(1, spec.Burst).Select(i => (Token: $"LOAD-{runTag}-{i:D4}", Group: groups[(i - 1) % groups.Count])).ToList();

            // Fire every bat at once, like an alarm storm in Akara.
            var gate = new TaskCompletionSource();
            var tasks = alarms.Select(a => Task.Run(async () =>
            {
                await gate.Task;
                return await site.RunBatAsync($"{a.Token} תקלה בלחץ המערכת", a.Group.GroupId.ToString(), "99.9");
            })).ToList();
            var t0 = DateTime.UtcNow;
            gate.SetResult();
            var bats = await Task.WhenAll(tasks);
            var burstEnd = bats.Max(b => b.EndedUtc);
            var burstStart = bats.Min(b => b.StartedUtc);

            result.BurstWallSeconds = Math.Round((burstEnd - burstStart).TotalSeconds, 2);
            result.AlarmsPerSecond = Math.Round(spec.Burst / Math.Max(0.001, result.BurstWallSeconds), 2);
            result.BatLatenciesMs = bats.Select(b => Math.Round(b.DurationMs, 1)).ToArray();
            result.BatStartOffsetsMs = bats.Select(b => Math.Round((b.StartedUtc - burstStart).TotalMilliseconds, 1)).ToArray();
            result.BatExitCodes = bats.Select(b => b.ExitCode).ToArray();
            result.BatLatencyMs = Stats(bats.Select(b => b.DurationMs));
            result.BatLatencyExit0Ms = bats.Any(b => b.ExitCode == 0) ? Stats(bats.Where(b => b.ExitCode == 0).Select(b => b.DurationMs)) : null;
            result.ExitCodes = bats.GroupBy(b => b.ExitCode).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count());
            var acceptedInBurst = site.Mock.Requests.Count(r => r.Accepted && r.ReceivedAtUtc <= burstEnd);
            result.SmsPerSecondDuringBurst = Math.Round(acceptedInBurst / Math.Max(0.001, result.BurstWallSeconds), 2);

            // Expected alarm x recipient pairs, as the provider must see them.
            string Wire(string phone) => spec.Kind == "SOAP" ? phone : "+972" + phone[1..];
            var expected = alarms.SelectMany(a => a.Group.Members.Select(m => (a.Token, Phone: Wire(m.Phone)))).ToHashSet();
            result.ExpectedSms = expected.Count;

            Dictionary<(string, string), int> AcceptedPairs() => site.Mock.Requests
                .Where(r => r.Accepted && r.Message != null && r.Phone != null && TokenRx.IsMatch(r.Message))
                .GroupBy(r => (TokenRx.Match(r.Message!).Value, r.Phone!))
                .ToDictionary(g => g.Key, g => g.Count());

            var converged = await Site.WaitUntilAsync(() =>
            {
                var accepted = AcceptedPairs();
                return Task.FromResult(site.SpoolFiles().Length == 0 && expected.All(e => accepted.ContainsKey(e)));
            }, spec.ConvergeTimeout ?? TimeSpan.FromSeconds(site.Options.SpoolMinAgeSeconds + 180), 1000);
            result.SecondsToFullDelivery = converged ? Math.Round((DateTime.UtcNow - t0).TotalSeconds, 1) : -1;
            if (!converged) result.Notes.Add("Did not converge to full delivery within the timeout.");

            // Count only once nothing moves any more: no in-flight mock call, and request count plus DB
            // delivery state unchanged for 3 consecutive polls (bounded at 60 s).
            var settleStart = DateTime.UtcNow;
            string? last = null;
            var stableFor = 0;
            while (DateTime.UtcNow - settleStart < TimeSpan.FromSeconds(60) && stableFor < 3)
            {
                await Task.Delay(500);
                var dbState = (await TestDb.DeliveriesForGroupsAsync(groups.Select(g => g.GroupId)))
                    .GroupBy(r => r.Status).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}");
                var snapshot = $"{site.Mock.Requests.Count}|{site.Mock.InFlight}|{string.Join(",", dbState)}";
                stableFor = site.Mock.InFlight == 0 && snapshot == last ? stableFor + 1 : 0;
                last = snapshot;
            }
            result.SettleSeconds = Math.Round((DateTime.UtcNow - settleStart).TotalSeconds, 1);
            if (stableFor < 3) result.Notes.Add("Provider/DB state did not settle within 60 s; duplicate count may be incomplete.");
            var requests = site.Mock.Requests;
            var pairs = AcceptedPairs();
            result.ProviderRequests = requests.Count;
            result.ProviderAccepted = requests.Count(r => r.Accepted);
            result.ProviderRefused = requests.Count(r => !r.Accepted && r.ResponseStatus > 0);
            result.ProviderNoAnswer = requests.Count(r => r.ResponseStatus == 0);
            result.ProviderContractErrors = requests.Count(r => r.ContractErrors.Count > 0);
            result.ProviderServiceMs = requests.Count > 0 ? Stats(requests.Select(r => r.DurationMs)) : null;
            result.MaxProviderConcurrency = MaxOverlap(requests);
            result.UniquePairsAccepted = expected.Count(e => pairs.ContainsKey(e));
            var missing = expected.Where(e => !pairs.ContainsKey(e)).ToList();
            result.MissingPairs = missing.Count;
            result.MissingSample = missing.Take(10).Select(m => $"{m.Token} -> {m.Phone}").ToList();
            result.DuplicateAcceptedSms = pairs.Where(p => expected.Contains(p.Key)).Sum(p => p.Value - 1);

            var rows = await TestDb.DeliveriesForGroupsAsync(groups.Select(g => g.GroupId));
            result.DbRows = rows.Count;
            result.DbRowsSuccess = rows.Count(r => r.Status == CriticalDeliveryStatus.Success);
            result.DbStatus = rows.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());
            result.DbDistinctAlarms = rows.Select(r => r.AlarmId).Distinct().Count();
            result.SpoolResidual = site.SpoolFiles().Length;
            result.SpoolFailed = site.SpoolSubfolderFiles("failed").Length;
            result.SpoolBad = site.SpoolSubfolderFiles("bad").Length;
            result.BatLogLines = site.BatLogLines().Length - warmLogLines;

            result.HarnessTimeouts = site.HarnessTimeouts.Count;
            result.ZeroLoss = result.HarnessTimeouts == 0 && result.MissingPairs == 0 && result.SpoolResidual == 0 && result.SpoolFailed == 0 && result.SpoolBad == 0 &&
                              result.DbRowsSuccess == result.ExpectedSms && result.DbDistinctAlarms == spec.Burst;
            if (result.BatLogLines != spec.Burst)
                result.Notes.Add($"Scada_sms.log has {result.BatLogLines} lines for {spec.Burst} calls (concurrent appends).");
            result.NoDuplicates = result.DuplicateAcceptedSms == 0;
            result.Verdict = !result.ZeroLoss ? "FAIL" : result.NoDuplicates ? "PASS" : "DEGRADED";
            result.Status = result.ZeroLoss ? "passed" : "failed";
            if (result.HarnessTimeouts > 0)
                result.Notes.Add($"{result.HarnessTimeouts} bat call(s) did not exit within {site.BatHarnessTimeout.TotalSeconds:0} s and were killed.");
            if (!result.ZeroLoss)
                site.KeepFiles = true;
            if (!result.ZeroLoss)
                result.Failure = $"Zero-loss check failed: harness-killed bats {result.HarnessTimeouts}, missing {result.MissingPairs}, spool residual {result.SpoolResidual}, failed {result.SpoolFailed}, bad {result.SpoolBad}, DB success {result.DbRowsSuccess}/{result.ExpectedSms}, DB alarms {result.DbDistinctAlarms}/{spec.Burst}";
            return result;
        }
        catch (Exception ex)
        {
            result.Status = "failed";
            result.Failure = ex.Message;
            site.KeepFiles = true;
            throw;
        }
        finally
        {
            ScenarioEvidence.WriteJson(Path.Combine("load", $"{spec.Kind}-{spec.Id}.json"), result);
        }
    }

    public static Percentiles Stats(IEnumerable<double> values)
    {
        var v = values.OrderBy(x => x).ToArray();
        double P(double p) => v[Math.Clamp((int)Math.Ceiling(p * v.Length) - 1, 0, v.Length - 1)];
        return new Percentiles(Math.Round(P(0.50), 1), Math.Round(P(0.95), 1), Math.Round(P(0.99), 1),
            Math.Round(v[^1], 1), Math.Round(v.Average(), 1), Math.Round(v[0], 1));
    }

    private static int MaxOverlap(IReadOnlyList<RecordedRequest> requests)
    {
        var events = requests.SelectMany(r => new[] { (T: r.ReceivedAtUtc, D: 1), (T: r.ReceivedAtUtc.AddMilliseconds(r.DurationMs), D: -1) })
            .OrderBy(e => e.T).ThenBy(e => e.D);
        int current = 0, max = 0;
        foreach (var e in events) { current += e.D; max = Math.Max(max, current); }
        return max;
    }
}
