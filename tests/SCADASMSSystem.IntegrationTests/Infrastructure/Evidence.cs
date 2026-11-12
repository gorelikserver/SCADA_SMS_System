using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SCADASMSSystem.IntegrationTests.Mock;

namespace SCADASMSSystem.IntegrationTests.Infrastructure;

/// <summary>A product finding observed by a test (bug, gap or risk), surfaced in the report.</summary>
public sealed record Finding(string Id, string Severity, string Title, string Detail);

/// <summary>
/// Evidence for one scenario: checks with expected/actual, captured provider payloads, bat results,
/// spool and DB observations. Written as JSON to the results folder for the report generator.
/// </summary>
public sealed class ScenarioEvidence
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Status { get; set; } = "running";
    /// <summary>True when the scenario passed by documenting a product finding (shown as FINDING, not as a plain pass).</summary>
    public bool DocumentsFinding => Findings.Count > 0;
    public string? Failure { get; set; }
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
    public DateTime? EndedUtc { get; set; }
    public object? Config { get; set; }
    public List<Check> Checks { get; } = new();
    public List<string> Steps { get; } = new();
    public List<BatResult> BatRuns { get; } = new();
    public List<RecordedRequest> ProviderRequests { get; set; } = new();
    public Dictionary<string, object?> Observations { get; } = new();
    public List<Finding> Findings { get; } = new();

    public sealed record Check(string Name, bool Passed, string? Expected, string? Actual);

    public void Step(string text) => Steps.Add($"{DateTime.UtcNow:HH:mm:ss.fff} {text}");

    public void Observe(string key, object? value) => Observations[key] = value;

    /// <summary>Records a check and fails the test (xUnit) when it does not hold.</summary>
    public void Check_(string name, bool passed, object? expected = null, object? actual = null)
    {
        Checks.Add(new Check(name, passed, expected?.ToString(), actual?.ToString()));
        if (!passed)
            throw new Xunit.Sdk.XunitException($"Check failed: {name}. Expected: {expected}. Actual: {actual}");
    }

    public void Equal<T>(string name, T expected, T actual) =>
        Check_(name, EqualityComparer<T>.Default.Equals(expected, actual), expected, actual);

    public void True(string name, bool condition, object? actual = null) => Check_(name, condition, true, actual ?? condition);

    public void Finding(string id, string severity, string title, string detail) =>
        Findings.Add(new Finding(id, severity, title, detail));

    public void Save()
    {
        var dir = Path.Combine(MockIt.ResultsDir, "scenarios");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{Kind}-{Id}.json"), JsonSerializer.Serialize(this, Json));
    }

    public static void WriteJson(string relativePath, object value)
    {
        var path = Path.Combine(MockIt.ResultsDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Json));
    }

    /// <summary>
    /// Runs a scenario body with a fresh site, records evidence and always saves it, then rethrows
    /// failures so xUnit reports them.
    /// </summary>
    public static async Task RunAsync(string kind, string id, string title, string description,
        Func<Site, ScenarioEvidence, Task> body, SiteOptions? options = null, bool startApp = true)
    {
        var ev = new ScenarioEvidence { Id = id, Kind = kind, Title = title, Description = description };
        Site? site = null;
        try
        {
            site = await Site.CreateAsync(kind, id, options);
            ev.Config = site.DescribeConfig();
            if (startApp)
            {
                await site.StartAppAsync();
                ev.Step($"Web app started at {site.ServiceUrl}; mock at {site.Mock.BaseUrl}");
            }
            await body(site, ev);
            var killed = site.HarnessTimeouts;
            if (killed.Count > 0)
            {
                ev.BatRuns.AddRange(killed.Where(k => !ev.BatRuns.Contains(k)));
                throw new Xunit.Sdk.XunitException($"{killed.Count} bat call(s) did not exit within {site.BatHarnessTimeout.TotalSeconds:0} s and were killed (process tree).");
            }
            ev.Status = "passed";
        }
        catch (Exception ex)
        {
            ev.Status = "failed";
            ev.Failure = ex.Message;
            if (site != null)
            {
                site.KeepFiles = true;
                ev.Observe("keptSiteFolder", site.Root);
            }
            throw;
        }
        finally
        {
            ev.EndedUtc = DateTime.UtcNow;
            if (site != null)
            {
                ev.ProviderRequests = site.Mock.Requests.ToList();
                ev.Observe("batLog", site.BatLogLines());
                await site.DisposeAsync();
            }
            ev.Save();
        }
    }
}
