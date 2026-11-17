using System.Text.Json.Nodes;
using Xunit;

namespace SCADASMSSystem.IntegrationTests;

/// <summary>
/// Go/no-go logic of the report generator, on synthetic evidence (no app, no DB). Plain GO needs a
/// complete, fully passing evidence set; anything degraded, indeterminate, failed or missing must prevent it.
/// </summary>
[Trait("Category", "ReportLogic")]
public class ReportVerdictTests
{
    private static readonly string[] MandatoryScenarios = { "S01-x", "S02-x", "S03-x", "S04-x", "S06-x", "S12-x", "S13-x" };
    private static readonly string[] LoadCases = { "L1-x", "L2-x", "L3-x", "L4-x", "L5-x", "L6-x" };

    private static JsonObject Scenario(string kind, string id, string status = "passed", string? finding = null)
    {
        var o = new JsonObject { ["Kind"] = kind, ["Id"] = id, ["Title"] = id, ["Status"] = status, ["Findings"] = new JsonArray() };
        if (finding != null) o["Findings"]!.AsArray().Add(new JsonObject { ["Id"] = finding, ["Severity"] = "Medium", ["Title"] = "t", ["Detail"] = "d" });
        return o;
    }

    private static JsonObject Load(string kind, string id, string verdict = "PASS", int duplicates = 0) => new()
    {
        ["Kind"] = kind, ["Id"] = id, ["Title"] = id, ["Burst"] = 10,
        ["Status"] = verdict is "PASS" or "DEGRADED" ? "passed" : verdict == "INDETERMINATE" ? "indeterminate" : "failed",
        ["Verdict"] = verdict, ["ZeroLoss"] = verdict != "FAIL", ["DuplicateAcceptedSms"] = duplicates,
        ["BatLogLines"] = 10, ["Notes"] = new JsonArray(),
    };

    private static (List<JsonObject> Scenarios, List<JsonObject> Loads, List<TestCounts> Counts, JsonObject Env) Complete()
    {
        var scenarios = new List<JsonObject>();
        foreach (var kind in new[] { "REST", "SOAP" })
            scenarios.AddRange(MandatoryScenarios.Select(id => Scenario(kind, id)));
        scenarios.Add(Scenario("SOAP", "S15-x", finding: "F-STALE-SENDING"));
        var loads = new List<JsonObject>();
        foreach (var kind in new[] { "REST", "SOAP" })
            loads.AddRange(LoadCases.Select(id => Load(kind, id)));
        var counts = new List<TestCounts> { new("unit-tests", 166, 166, 0, 0), new("scenarios", 15, 15, 0, 0) };
        var env = new JsonObject { ["phaseFailures"] = new JsonArray() };
        return (scenarios, loads, counts, env);
    }

    private static Analysis.VerdictResult Verdict((List<JsonObject> Scenarios, List<JsonObject> Loads, List<TestCounts> Counts, JsonObject Env) e) =>
        Analysis.ComputeVerdict(ReportData.From(e.Env, e.Scenarios, e.Loads, e.Counts));

    [Fact]
    public void Complete_passing_evidence_is_go_and_passing_finding_scenarios_are_allowed()
    {
        var e = Complete();
        e.Scenarios.Add(Scenario("REST", "S11-x", finding: "F-MSG-LENGTH"));
        var v = Verdict(e);
        Assert.Equal(Analysis.VerdictLevel.Go, v.Level);
        Assert.Empty(v.Reasons);
    }

    [Fact]
    public void Degraded_load_run_is_at_best_go_with_reservations()
    {
        var e = Complete();
        e.Loads[2] = Load("REST", "L3-x", "DEGRADED", duplicates: 7);
        var data = ReportData.From(e.Env, e.Scenarios, e.Loads, e.Counts);
        Assert.Equal("DEGRADED", data.Checklist.Single(c => c.Evidence == "L1-L3").Status);
        var v = Analysis.ComputeVerdict(data);
        Assert.Equal(Analysis.VerdictLevel.GoWithReservations, v.Level);
        Assert.StartsWith("GO WITH RESERVATIONS", v.Text);
    }

    [Fact]
    public void Indeterminate_load_run_is_no_go()
    {
        var e = Complete();
        e.Loads[^1] = Load("SOAP", "L6-x", "INDETERMINATE");
        var data = ReportData.From(e.Env, e.Scenarios, e.Loads, e.Counts);
        Assert.Equal("INDETERMINATE", data.Checklist.Single(c => c.Evidence == "L4-L6").Status);
        Assert.Equal(Analysis.VerdictLevel.NoGo, Analysis.ComputeVerdict(data).Level);
    }

    [Fact]
    public void Failed_scenario_outside_the_checklist_is_no_go()
    {
        var e = Complete();
        e.Scenarios.Add(Scenario("REST", "S10-x", status: "failed"));
        var v = Verdict(e);
        Assert.Equal(Analysis.VerdictLevel.NoGo, v.Level);
        Assert.Contains(v.Reasons, r => r.Contains("S10-x"));
    }

    [Fact]
    public void Failed_finding_scenario_is_a_failure_not_a_finding()
    {
        var e = Complete();
        e.Scenarios.Add(Scenario("SOAP", "S16-x", status: "failed", finding: "F-SOAP-PLACEHOLDERS"));
        var v = Verdict(e);
        Assert.Equal(Analysis.VerdictLevel.NoGo, v.Level);
        Assert.Contains(v.Reasons, r => r.Contains("S16-x") && r.Contains("FINDING"));
    }

    [Fact]
    public void Failed_trx_count_is_no_go()
    {
        var e = Complete();
        e.Counts.Add(new TestCounts("mock-self-tests", 15, 14, 1, 0));
        Assert.Equal(Analysis.VerdictLevel.NoGo, Verdict(e).Level);
    }

    [Fact]
    public void Phase_failure_is_no_go()
    {
        var e = Complete();
        e.Env["phaseFailures"] = new JsonArray("load (exit 1)");
        Assert.Equal(Analysis.VerdictLevel.NoGo, Verdict(e).Level);
    }

    [Fact]
    public void Phase_failure_as_single_string_is_no_go()
    {
        var e = Complete();
        e.Env["phaseFailures"] = "scenarios (exit 1)";
        Assert.Equal(Analysis.VerdictLevel.NoGo, Verdict(e).Level);
    }

    [Fact]
    public void Missing_soap_result_for_a_mandatory_case_is_not_run_and_no_go()
    {
        var e = Complete();
        e.Scenarios.RemoveAll(s => s["Kind"]!.ToString() == "SOAP" && s["Id"]!.ToString() == "S02-x");
        e.Loads.RemoveAll(l => l["Kind"]!.ToString() == "SOAP" && l["Id"]!.ToString() == "L4-x");
        var data = ReportData.From(e.Env, e.Scenarios, e.Loads, e.Counts);
        Assert.Contains(data.Checklist, c => c.Evidence == "S02" && c.Status == "NOT RUN");
        Assert.Contains(data.Checklist, c => c.Evidence == "L4-L6" && c.Status == "NOT RUN");
        Assert.Equal(Analysis.VerdictLevel.NoGo, Analysis.ComputeVerdict(data).Level);
    }

    [Fact]
    public void Failed_load_run_is_no_go()
    {
        var e = Complete();
        e.Loads[0] = Load("REST", "L1-x", "FAIL");
        Assert.Equal(Analysis.VerdictLevel.NoGo, Verdict(e).Level);
    }
}
