using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

// Offline report for the mock integration suite: reads the evidence JSON written by
// SCADASMSSystem.IntegrationTests (scenarios/*.json, load/*.json), the .trx files and env.json,
// and writes report.html (self-contained: inline CSS and SVG, no CDN) and summary.md.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

if (args.Length < 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: SCADASMSSystem.MockReport <results folder>");
    return 2;
}

var dir = args[0];
var report = ReportData.Load(dir);
File.WriteAllText(Path.Combine(dir, "report.html"), HtmlReport.Render(report), new UTF8Encoding(false));
File.WriteAllText(Path.Combine(dir, "summary.md"), MarkdownReport.Render(report), new UTF8Encoding(false));
Console.WriteLine($"Report written: {Path.Combine(dir, "report.html")}");
Console.WriteLine($"Summary written: {Path.Combine(dir, "summary.md")}");
return 0;

sealed record TestCounts(string Name, int Total, int Passed, int Failed, int Skipped);
sealed record FindingItem(string Id, string Severity, string Title, string Detail, string Source);
sealed record CheckItem(string Area, string Item, string Status, string Evidence);

sealed class ReportData
{
    public JsonObject Env { get; init; } = new();
    public List<JsonObject> Scenarios { get; init; } = new();
    public List<JsonObject> Loads { get; init; } = new();
    public List<TestCounts> Counts { get; init; } = new();
    public List<FindingItem> Findings { get; init; } = new();
    public List<CheckItem> Checklist { get; init; } = new();
    public string Folder { get; init; } = "";

    public static ReportData Load(string dir)
    {
        static List<JsonObject> ReadAll(string path) => Directory.Exists(path)
            ? Directory.GetFiles(path, "*.json").OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => JsonNode.Parse(File.ReadAllText(f))!.AsObject()).ToList()
            : new List<JsonObject>();

        var env = File.Exists(Path.Combine(dir, "env.json"))
            ? JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "env.json")))!.AsObject()
            : new JsonObject();
        var scenarios = ReadAll(Path.Combine(dir, "scenarios"));
        var loads = ReadAll(Path.Combine(dir, "load"));
        var counts = new List<TestCounts>();
        foreach (var trx in Directory.Exists(dir) ? Directory.GetFiles(dir, "*.trx", SearchOption.AllDirectories) : [])
        {
            var c = XDocument.Load(trx).Descendants().FirstOrDefault(e => e.Name.LocalName == "Counters");
            if (c == null) continue;
            int A(string n) => int.TryParse(c.Attribute(n)?.Value, out var v) ? v : 0;
            var total = A("total");
            var executed = A("executed");
            counts.Add(new TestCounts(Path.GetFileNameWithoutExtension(trx), total, A("passed"), A("failed"), total - executed));
        }

        var data = new ReportData { Env = env, Scenarios = scenarios, Loads = loads, Counts = counts, Folder = dir };
        data.Findings.AddRange(Analysis.Findings(data));
        data.Checklist.AddRange(Analysis.Checklist(data));
        return data;
    }

    public JsonObject? Scenario(string kind, string idPrefix) =>
        Scenarios.FirstOrDefault(s => S(s, "Kind") == kind && S(s, "Id").StartsWith(idPrefix, StringComparison.Ordinal));

    public JsonObject? LoadRun(string kind, string idPrefix) =>
        Loads.FirstOrDefault(s => S(s, "Kind") == kind && S(s, "Id").StartsWith(idPrefix, StringComparison.Ordinal));

    public static string S(JsonNode? o, string key) => o?[key]?.ToString() ?? "";
    public static double D(JsonNode? o, string key) => o?[key] is JsonValue v && v.TryGetValue<double>(out var d) ? d : double.NaN;
    public static bool Passed(JsonNode? o) => S(o, "Status") == "passed";
}

static class Analysis
{
    // Findings that come from reading the code and spec while building the suite; each one is
    // re-stated with the run's numbers where the run measured it.
    public static IEnumerable<FindingItem> Findings(ReportData d)
    {
        var list = new List<FindingItem>();

        foreach (var s in d.Scenarios)
            foreach (var f in s["Findings"]?.AsArray() ?? new JsonArray())
            {
                var id = ReportData.S(f, "Id");
                if (list.Any(x => x.Id == id)) continue;
                list.Add(new FindingItem(id, ReportData.S(f, "Severity"), ReportData.S(f, "Title"), ReportData.S(f, "Detail"),
                    $"scenario {ReportData.S(s, "Kind")} {ReportData.S(s, "Id")}"));
            }

        var l7 = d.LoadRun("SOAP", "L7");
        var l7Detail = l7 == null ? "Not measured in this run." :
            $"Measured with the shipped defaults, 10 alarms x 3 recipients: bat exit codes {ExitCodes(l7)}, p95 bat latency {Ms(l7?["BatLatencyMs"], "P95")}, " +
            $"full delivery after {ReportData.D(l7, "SecondsToFullDelivery"):0} s.";
        list.Add(new FindingItem("F-RATE-LIMIT", "High", "Global rate limit (10 SMS / 60 s by default) throttles an alarm storm",
            "SmsSettings.RateLimit/RateWindow is one static counter for the whole service, applied to critical alarms (WaitForRateLimitAsync). " +
            "With the defaults a storm of 50 alarms x 3 recipients needs 15 minutes; bats time out after MAXTIME (120 s) and the rest is delivered " +
            "late from the spool. Size RateLimit for the site's worst storm and the provider's real limit before go-live. " + l7Detail, "code + load L7"));

        list.Add(new FindingItem("F-TIME-BUDGET", "Medium", "Per-recipient retry budget can exceed the bat MAXTIME",
            "Recipients are sent one after another; each can take TimeoutSeconds x (1 + RetryAttempts) plus back-off (defaults: 30 s x 4 = 120 s+). " +
            "A slow or hanging provider with a 3-recipient group keeps the HTTP request open far beyond the bat's MAXTIME (120 s), so the bat exits 2 " +
            "while the service is still sending. Delivery still completes (spool + idempotency), but the bat log then says 'unreachable'. " +
            "Keep TimeoutSeconds x (1 + RetryAttempts) x group size below MAXTIME, or send recipients in parallel.", "code + scenario S06/S14"));

        list.Add(new FindingItem("F-TIMESTAMP-RETRY", "Low", "Retried SMS carry a new timestamp",
            "SmsService prefixes '[dd/MM HH:mm]' at send time, so a recipient retried from the spool minutes later gets a different time than the others " +
            "(the alarm time is not ReceivedAt from the spool file). Consider using ReceivedAt.", "code"));

        list.Add(new FindingItem("F-SOAP-NO-IDEMPOTENCY", "Medium", "Sheba contract has no idempotency/reference field",
            "SendSMS carries no AlarmId or message reference, so the provider cannot de-duplicate. Every INDETERMINATE retry (timeout, dropped connection) " +
            "that had in fact been delivered becomes a duplicate SMS. Accepted by design (a duplicate beats a missed alarm); count them on site.", "spec"));

        list.Add(new FindingItem("F-SOAP-FAILURE-FORMAT", "Medium", "Sheba failure response is not specified",
            "The spec shows only the success envelope. The profile treats <Success>true</Success> as the only success and a non-empty <ErrorMessage> " +
            "or a SOAP Fault as failure; the mock models failure as <Success>false</Success> + ErrorMessage on HTTP 200 and auth failure as a SOAP Fault on HTTP 500. " +
            "Confirm real failure and authentication-error responses against the Ensemble TEST environment.", "spec"));

        list.Add(new FindingItem("F-TLS", "High", "HTTPS to Sheba and the Sheba certificate are untested",
            "Sheba endpoints are HTTPS-only on 443 and need the Sheba general certificate installed plus a firewall opening. The mock is plain HTTP on loopback, " +
            "so TLS trust, proxy and firewall are not covered by this suite. Verify against the Ensemble TEST endpoint from the site server before go-live.", "spec"));

        list.Add(new FindingItem("F-MESSAGETYPE", "Medium", "MessageType and SendingSystem must come from Sheba",
            "MessageType is a customer-specific value assigned by Sheba (sample 'SmsType1'); a wrong value is a refusal on every alarm. The suite uses profile " +
            "variables MessageType=SmsType1 and SendingSystem=SCADA-SMS. Obtain the real values (and the Username) from Sheba and set them in the profile.", "spec"));

        list.Add(new FindingItem("F-AKARA-QUOTING", "Low", "Akara's exact Run command line is assumed",
            "The suite starts the bat as cmd.exe /c \"Scada_sms.bat\" \"DESCRIPTION\" GROUP \"ALM_VALUE\". The real command comes from Akara evaluating " +
            "the alarm-action template (AlarmActionService.BuildSmsCommandForGroupId); its quoting of DESCRIPTION was not reproduced. Fire one real Akara alarm on site " +
            "and compare Scada_sms.log and the delivered text.", "code"));

        var logRuns = d.Loads.Where(l => ReportData.D(l, "BatLogLines") < ReportData.D(l, "Burst")).ToList();
        if (logRuns.Count > 0)
        {
            var lines = d.Loads.Sum(l => ReportData.D(l, "BatLogLines"));
            var calls = d.Loads.Sum(l => ReportData.D(l, "Burst"));
            list.Add(new FindingItem("F-BATLOG-CONCURRENCY", "Medium", "Scada_sms.log loses lines under concurrent bat calls",
                $"Across all load runs Scada_sms.log got {lines:0} lines for {calls:0} bat calls ({100 * (1 - lines / calls):0}% lost; worst: " +
                string.Join(", ", logRuns.OrderBy(l => ReportData.D(l, "BatLogLines") / ReportData.D(l, "Burst")).Take(3)
                    .Select(l => $"{ReportData.S(l, "Kind")} {ReportData.S(l, "Id")} {ReportData.D(l, "BatLogLines")}/{ReportData.D(l, "Burst")}")) +
                "). Every alarm was still delivered, but the log is the only proof an alarm reached the system (Akara keeps none): concurrent '>>' appends " +
                "from cmd.exe fail while another bat holds the file. Append with a retry (or via PowerShell with a shared-write open).", "load"));
        }

        foreach (var l in d.Loads.Where(l => !ReportData.Passed(l)))
            list.Add(new FindingItem("F-LOSS-" + ReportData.S(l, "Kind") + ReportData.S(l, "Id"), "High", $"Zero-loss check failed in {ReportData.S(l, "Kind")} {ReportData.S(l, "Id")}",
                ReportData.S(l, "Failure"), $"load {ReportData.S(l, "Id")}"));

        var dupRuns = d.Loads.Where(x => ReportData.D(x, "DuplicateAcceptedSms") > 0).ToList();
        if (dupRuns.Count > 0)
            list.Add(new FindingItem("F-DUPLICATES", "Low", "Duplicate SMS when a provider answer is slower than TimeoutSeconds",
                string.Join(" ", dupRuns.Select(x => $"{ReportData.S(x, "Kind")} {ReportData.S(x, "Id")}: {ReportData.D(x, "DuplicateAcceptedSms")} duplicate(s), slowest provider answer {Analysis.Ms(x["ProviderServiceMs"], "Max")}.")) +
                " The provider accepted the message but answered after the client timeout (3 s in this suite), so the app recorded INDETERMINATE and re-sent: " +
                "the designed trade-off (a duplicate beats a missed alarm). In the 200-alarm runs the slow answers come from the test machine itself being saturated " +
                "(200 bat processes plus the mock on one host); on site keep TimeoutSeconds well above the provider's worst response time.", "load"));

        var order = new Dictionary<string, int> { ["High"] = 0, ["Medium"] = 1, ["Low"] = 2 };
        return list.OrderBy(f => order.GetValueOrDefault(f.Severity, 3)).ThenBy(f => f.Id);
    }

    public static IEnumerable<CheckItem> Checklist(ReportData d)
    {
        // A mandatory case counts only when BOTH provider kinds ran it: a missing kind is NOT RUN, never PASS.
        string Both(string prefix)
        {
            var r = d.Scenario("REST", prefix);
            var s = d.Scenario("SOAP", prefix);
            if (r == null || s == null) return "NOT RUN";
            return ReportData.Passed(r) && ReportData.Passed(s) ? "PASS" : "FAIL";
        }
        string Load(params string[] prefixes)
        {
            var status = "PASS";
            foreach (var prefix in prefixes)
                foreach (var kind in new[] { "REST", "SOAP" })
                {
                    var run = d.LoadRun(kind, prefix);
                    if (run == null) return "NOT RUN";
                    if (!ReportData.Passed(run)) status = "FAIL";
                }
            return status;
        }

        yield return new("Contract", "Exact SOAP envelope, SOAPAction, WS-Security and REST JSON match the profile/spec", Both("S01"), "S01 payload checks");
        yield return new("Contract", "Bat exits 0 only after every recipient was accepted", Both("S01"), "S01, S14");
        yield return new("Contract", "Partial failure = exit 3; only failed recipients retried with the same AlarmId", Both("S02"), "S02");
        yield return new("Contract", "HTTP 200 with a refusal in the body is a failure (SOAP Success=false)", Both("S03"), "S03");
        yield return new("Contract", "Service down: alarm spooled, delivered when the service starts", Both("S04"), "S04");
        yield return new("Contract", "Timeout = INDETERMINATE, re-sent from spool", Both("S06"), "S06");
        yield return new("Contract", "Idempotency: same AlarmId never re-sent to an accepted recipient", Both("S12"), "S12");
        yield return new("Contract", "DESCRIPTION (Hebrew, cmd.exe special characters) reaches the provider unchanged", Both("S13"), "S13");
        yield return new("Resilience", "Service crash mid-flight recovered without loss", d.Scenario("SOAP", "S15") is { } c ? (ReportData.Passed(c) ? "PASS" : "FAIL") : "NOT RUN", "S15");
        yield return new("Load", "Zero loss, healthy provider, bursts of 10/50/200 (REST and SOAP)", Load("L1", "L2", "L3"), "L1-L3");
        yield return new("Load", "Zero loss with latency, 5xx and timeouts (REST and SOAP)", Load("L4", "L5", "L6"), "L4-L6");
        yield return new("Site", "RateLimit/RateWindow sized for the worst alarm storm and the provider limit", "OPEN", "F-RATE-LIMIT, L7");
        yield return new("Site", "HTTPS to Ensemble TEST endpoint works from the site server (Sheba certificate installed, firewall 443 open)", "OPEN", "F-TLS");
        yield return new("Site", "Real Username/Password, MessageType and SendingSystem from Sheba set in the profile", "OPEN", "F-MESSAGETYPE");
        yield return new("Site", "Profile verified with a real test SMS to an engineer's phone against Ensemble TEST, then activated", "OPEN", "Settings > SMS Provider");
        yield return new("Site", "Sheba failure and auth-error responses observed and matched by the profile rules", "OPEN", "F-SOAP-FAILURE-FORMAT");
        yield return new("Site", "TZ/FirstName/LastName placeholder policy agreed with Sheba", "OPEN", "F-SOAP-PLACEHOLDERS");
        yield return new("Site", "One real Akara alarm fired; Scada_sms.log line and delivered text checked", "OPEN", "F-AKARA-QUOTING");
        yield return new("Site", "PCIMUTIL copy of Scada_sms.bat current (no red banner) and SPOOL_ROOT = ScadaSpool:Root", "OPEN", "CONTEXT.md packaging");
    }

    /// <summary>Every automated (non-site) checklist item must be PASS; FAIL or NOT RUN blocks GO.</summary>
    public static bool AutomatedComplete(ReportData d) => d.Checklist.Where(c => c.Area != "Site").All(c => c.Status == "PASS");

    public static string Verdict(ReportData d) =>
        d.Checklist.Any(c => c.Status == "FAIL") ? "NO-GO (automated checks failed)"
        : !AutomatedComplete(d) ? "NO-GO (mandatory cases not run for both REST and SOAP)"
        : "GO for site integration (open site items and findings remain)";

    public static string LoadStatus(JsonNode? l) =>
        l == null ? "n/a" : !ReportData.Passed(l) ? "FAIL" : ReportData.D(l, "DuplicateAcceptedSms") > 0 ? "DEGRADED" : "PASS";

    public static string ExitCodes(JsonNode? l) =>
        string.Join(", ", (l?["ExitCodes"]?.AsObject() ?? new JsonObject()).Select(kv => $"{kv.Key}: {kv.Value}"));

    public static string Ms(JsonNode? p, string key) =>
        p?[key] is JsonValue v && v.TryGetValue<double>(out var d) ? (d >= 10000 ? $"{d / 1000:0.0} s" : $"{d:0} ms") : "-";
}

static class HtmlReport
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    public static string Render(ReportData d)
    {
        var sb = new StringBuilder();
        var scenariosPassed = d.Scenarios.Count(ReportData.Passed);
        var loadsPassed = d.Loads.Count(ReportData.Passed);
        var automatedFail = !Analysis.AutomatedComplete(d);
        var verdict = Analysis.Verdict(d);
        var verdictClass = automatedFail ? "bad" : "good";

        sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<title>SCADA SMS Mock Integration</title><style>").Append(Css).Append("</style></head><body><main>");
        sb.Append("<header><h1>SCADA SMS — pre-site-integration mock suite</h1>");
        sb.Append($"<p class=\"sub\">Generated {E(ReportData.S(d.Env, "generatedAt"))} · commit {E(ReportData.S(d.Env, "gitSha"))} ({E(ReportData.S(d.Env, "gitBranch"))})</p>");
        sb.Append($"<div class=\"verdict {verdictClass}\">{E(verdict)}</div>");
        sb.Append("<div class=\"tiles\">");
        Tile(sb, "Scenarios", $"{scenariosPassed}/{d.Scenarios.Count}", "passed");
        Tile(sb, "Load runs", $"{loadsPassed}/{d.Loads.Count}", "zero-loss");
        var sms = d.Loads.Sum(l => ReportData.D(l, "ExpectedSms") is var x && !double.IsNaN(x) ? x : 0);
        Tile(sb, "SMS reconciled", sms.ToString("0"), "alarm × recipient pairs");
        Tile(sb, "Findings", d.Findings.Count.ToString(), $"{d.Findings.Count(f => f.Severity == "High")} high");
        sb.Append("</div></header>");

        sb.Append("<nav><a href=\"#env\">Environment</a><a href=\"#checklist\">Go/no-go</a><a href=\"#findings\">Findings</a><a href=\"#scenarios\">Scenarios</a><a href=\"#load\">Load</a></nav>");

        // Environment
        sb.Append("<section id=\"env\"><h2>Environment and configuration</h2><table class=\"kv\">");
        foreach (var kv in d.Env)
        {
            if (kv.Key is "testCounts") continue;
            sb.Append($"<tr><th>{E(kv.Key)}</th><td>{E(kv.Value?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }).Trim('"'))}</td></tr>");
        }
        sb.Append("</table>");
        if (d.Counts.Count > 0)
        {
            sb.Append("<h3>Test counts (from .trx)</h3><table><tr><th>Run</th><th>Total</th><th>Passed</th><th>Failed</th><th>Skipped</th></tr>");
            foreach (var c in d.Counts)
                sb.Append($"<tr><td>{E(c.Name)}</td><td>{c.Total}</td><td>{c.Passed}</td><td class=\"{(c.Failed > 0 ? "bad" : "")}\">{c.Failed}</td><td>{c.Skipped}</td></tr>");
            sb.Append("</table>");
        }
        foreach (var kind in new[] { "REST", "SOAP" })
        {
            var cfg = d.Scenario(kind, "S01")?["Config"];
            if (cfg == null) continue;
            sb.Append($"<h3>{kind} site configuration used by scenarios</h3><table class=\"kv\">");
            foreach (var kv in cfg.AsObject())
                sb.Append($"<tr><th>{E(kv.Key)}</th><td><code>{E(kv.Value?.ToJsonString().Trim('"'))}</code></td></tr>");
            sb.Append("</table>");
        }
        sb.Append("<p class=\"note\">Timings are shortened for the suite (TimeoutSeconds 3, RetryAttempts 1, bat MAXTIME 20 s, spool MinAge 25 s, poll 5 s, RateLimit 100000) except run L7, which uses the shipped defaults.</p>");
        sb.Append("</section>");

        // Checklist
        sb.Append("<section id=\"checklist\"><h2>Go/no-go checklist for site integration</h2><table><tr><th>Area</th><th>Item</th><th>Status</th><th>Evidence</th></tr>");
        foreach (var c in d.Checklist)
            sb.Append($"<tr><td>{E(c.Area)}</td><td>{E(c.Item)}</td><td><span class=\"pill {c.Status.ToLowerInvariant().Replace(' ', '-')}\">{E(c.Status)}</span></td><td>{E(c.Evidence)}</td></tr>");
        sb.Append("</table><p class=\"note\">PASS/FAIL items are proven by this run. OPEN items need the real site (TLS, credentials, Akara) and cannot be covered by a mock.</p></section>");

        // Findings
        sb.Append("<section id=\"findings\"><h2>Findings and risks</h2><table><tr><th>Id</th><th>Severity</th><th>Finding</th><th>Source</th></tr>");
        foreach (var f in d.Findings)
            sb.Append($"<tr><td><code>{E(f.Id)}</code></td><td><span class=\"pill sev-{f.Severity.ToLowerInvariant()}\">{E(f.Severity)}</span></td><td><b>{E(f.Title)}</b><br>{E(f.Detail)}</td><td>{E(f.Source)}</td></tr>");
        sb.Append("</table></section>");

        // Scenarios
        sb.Append("<section id=\"scenarios\"><h2>End-to-end scenarios</h2><table><tr><th>Scenario</th><th>REST</th><th>SOAP</th></tr>");
        foreach (var id in d.Scenarios.Select(s => ReportData.S(s, "Id")).Distinct().OrderBy(x => x, StringComparer.Ordinal))
        {
            var r = d.Scenarios.FirstOrDefault(s => ReportData.S(s, "Id") == id && ReportData.S(s, "Kind") == "REST");
            var s = d.Scenarios.FirstOrDefault(s => ReportData.S(s, "Id") == id && ReportData.S(s, "Kind") == "SOAP");
            var title = ReportData.S(s ?? r, "Title");
            sb.Append($"<tr><td><b>{E(id)}</b> {E(title)}</td><td>{Status(r)}</td><td>{Status(s)}</td></tr>");
        }
        sb.Append("</table>");
        foreach (var s in d.Scenarios.OrderBy(x => ReportData.S(x, "Id"), StringComparer.Ordinal).ThenBy(x => ReportData.S(x, "Kind")))
            ScenarioDetail(sb, s);
        sb.Append("</section>");

        // Load
        sb.Append("<section id=\"load\"><h2>Load tests (alarm storm)</h2>");
        sb.Append("<p>Every run fires N <code>Scada_sms.bat</code> processes at the same moment (Akara's 3 arguments, groups of 3 recipients round-robin over 5 groups), " +
                  "then reconciles mock records, DB delivery rows and the spool until every alarm × recipient was accepted. Latency = bat process start to exit; percentiles are nearest-rank (p95 = the value at rank ceil(0.95·N)). " +
                  "Zero-loss = every pair accepted, spool empty, DB all SUCCESS. No-dup = no pair accepted twice. A zero-loss run with duplicates is DEGRADED, not FAIL.</p>");
        sb.Append("<div class=\"scroll\"><table><tr><th>Run</th><th>N</th><th>Faults</th><th>Exit codes</th><th>p50</th><th>p95</th><th>p99</th><th>max</th><th>Alarms/s</th><th>SMS/s</th><th>Full delivery</th><th>Provider req.</th><th>Missing</th><th>Zero-loss</th><th>Dup.</th><th>No-dup</th><th>Status</th></tr>");
        foreach (var l in d.Loads.OrderBy(x => ReportData.S(x, "Kind")).ThenBy(x => ReportData.S(x, "Id"), StringComparer.Ordinal))
        {
            var lat = l["BatLatencyMs"];
            var faults = string.Join("; ", (l["Faults"]?.AsArray() ?? new JsonArray()).Select(x => x?.ToString()));
            if (ReportData.D(l, "ProviderLatencyMs") > 0) faults = $"latency {ReportData.D(l, "ProviderLatencyMs")} ms {faults}";
            sb.Append($"<tr><td><b>{E(ReportData.S(l, "Kind"))} {E(ReportData.S(l, "Id"))}</b></td><td>{ReportData.D(l, "Burst")}</td><td>{E(faults)}</td><td>{E(Analysis.ExitCodes(l))}</td>" +
                      $"<td>{Analysis.Ms(lat, "P50")}</td><td>{Analysis.Ms(lat, "P95")}</td><td>{Analysis.Ms(lat, "P99")}</td><td>{Analysis.Ms(lat, "Max")}</td>" +
                      $"<td>{ReportData.D(l, "AlarmsPerSecond"):0.0}</td><td>{ReportData.D(l, "SmsPerSecondDuringBurst"):0.0}</td><td>{Secs(ReportData.D(l, "SecondsToFullDelivery"))}</td>" +
                      $"<td>{ReportData.D(l, "ProviderRequests")} ({ReportData.D(l, "ProviderAccepted")} ok)</td><td>{ReportData.D(l, "MissingPairs")}</td><td>{(ReportData.Passed(l) ? "yes" : "<span class=\"bad\">no</span>")}</td><td>{ReportData.D(l, "DuplicateAcceptedSms")}</td><td>{(ReportData.D(l, "DuplicateAcceptedSms") > 0 ? "<span class=\"warn\">no</span>" : "yes")}</td><td>{LoadPill(l)}</td></tr>");
        }
        sb.Append("</table></div>");
        foreach (var kind in new[] { "REST", "SOAP" })
        {
            var runs = d.Loads.Where(l => ReportData.S(l, "Kind") == kind).OrderBy(x => ReportData.S(x, "Id"), StringComparer.Ordinal).ToList();
            if (runs.Count == 0) continue;
            sb.Append($"<h3>{kind}: bat latency percentiles (nearest-rank)</h3>").Append(Charts.PercentileBars(runs));
            sb.Append($"<h3>{kind}: per-alarm latency vs start offset</h3><div class=\"grid\">");
            foreach (var l in runs) sb.Append(Charts.Scatter(l));
            sb.Append("</div>");
        }
        foreach (var l in d.Loads.OrderBy(x => ReportData.S(x, "Kind")).ThenBy(x => ReportData.S(x, "Id"), StringComparer.Ordinal))
        {
            sb.Append($"<details><summary>{E(ReportData.S(l, "Kind"))} {E(ReportData.S(l, "Id"))} — {E(ReportData.S(l, "Title"))} — reconciliation</summary><table class=\"kv\">");
            foreach (var key in new[] { "ExpectedSms", "UniquePairsAccepted", "MissingPairs", "DuplicateAcceptedSms", "ProviderRequests", "ProviderAccepted", "ProviderRefused", "ProviderNoAnswer", "ProviderContractErrors", "MaxProviderConcurrency", "DbRows", "DbRowsSuccess", "DbDistinctAlarms", "SpoolResidual", "SpoolFailed", "SpoolBad", "BatLogLines", "BurstWallSeconds", "SecondsToFullDelivery" })
                sb.Append($"<tr><th>{key}</th><td>{E(l[key]?.ToJsonString())}</td></tr>");
            sb.Append($"<tr><th>DbStatus</th><td>{E(l["DbStatus"]?.ToJsonString())}</td></tr>");
            sb.Append($"<tr><th>ProviderServiceMs</th><td>{E(l["ProviderServiceMs"]?.ToJsonString())}</td></tr>");
            sb.Append($"<tr><th>Notes</th><td>{E(l["Notes"]?.ToJsonString())}</td></tr>");
            if (ReportData.S(l, "Failure") is { Length: > 0 } fail) sb.Append($"<tr><th>Failure</th><td class=\"bad\">{E(fail)}</td></tr>");
            sb.Append($"<tr><th>Config</th><td><code>{E(l["Config"]?.ToJsonString())}</code></td></tr></table></details>");
        }
        sb.Append("</section>");
        sb.Append($"<footer>Raw evidence: <code>{E(d.Folder)}</code> (scenarios/*.json, load/*.json, *.trx). No SMS was sent to any real phone or provider: every request went to the loopback mock.</footer>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static string Secs(double s) => double.IsNaN(s) ? "-" : s < 0 ? "<span class=\"bad\">not reached</span>" : $"{s:0.0} s";

    private static string Status(JsonNode? o) => o == null ? "<span class=\"pill not-run\">n/a</span>"
        : !ReportData.Passed(o) ? "<span class=\"pill fail\">FAIL</span>"
        : (o["Findings"]?.AsArray().Count ?? 0) > 0
            ? $"<span class=\"pill open\" title=\"Passed by documenting current product behavior\">FINDING {E(string.Join(", ", o["Findings"]!.AsArray().Select(f => ReportData.S(f, "Id"))))}</span>"
            : "<span class=\"pill pass\">PASS</span>";

    private static string LoadPill(JsonNode? l) => Analysis.LoadStatus(l) switch
    {
        "PASS" => "<span class=\"pill pass\">PASS</span>",
        "DEGRADED" => "<span class=\"pill open\" title=\"Zero loss, but duplicate SMS were accepted\">DEGRADED</span>",
        "FAIL" => "<span class=\"pill fail\">FAIL</span>",
        _ => "<span class=\"pill not-run\">n/a</span>",
    };

    private static void Tile(StringBuilder sb, string label, string value, string sub) =>
        sb.Append($"<div class=\"tile\"><div class=\"tl\">{E(label)}</div><div class=\"tv\">{E(value)}</div><div class=\"ts\">{E(sub)}</div></div>");

    private static void ScenarioDetail(StringBuilder sb, JsonObject s)
    {
        sb.Append($"<details id=\"{E(ReportData.S(s, "Kind") + "-" + ReportData.S(s, "Id"))}\"><summary>{Status(s)} <b>{E(ReportData.S(s, "Kind"))} {E(ReportData.S(s, "Id"))}</b> — {E(ReportData.S(s, "Title"))}</summary>");
        sb.Append($"<p>{E(ReportData.S(s, "Description"))}</p>");
        if (ReportData.S(s, "Failure") is { Length: > 0 } failure) sb.Append($"<p class=\"bad\">Failure: {E(failure)}</p>");
        var checks = s["Checks"]?.AsArray() ?? new JsonArray();
        sb.Append($"<h4>Checks ({checks.Count(c => c?["Passed"]?.GetValue<bool>() == true)}/{checks.Count} passed)</h4><table><tr><th>Check</th><th>Expected</th><th>Actual</th><th></th></tr>");
        foreach (var c in checks)
        {
            var ok = c?["Passed"]?.GetValue<bool>() == true;
            sb.Append($"<tr><td>{E(ReportData.S(c, "Name"))}</td><td><code>{E(Trim(ReportData.S(c, "Expected")))}</code></td><td><code>{E(Trim(ReportData.S(c, "Actual")))}</code></td><td>{(ok ? "✔" : "<span class=\"bad\">✘</span>")}</td></tr>");
        }
        sb.Append("</table>");
        var bats = s["BatRuns"]?.AsArray() ?? new JsonArray();
        if (bats.Count > 0)
        {
            sb.Append("<h4>Bat calls</h4><table><tr><th>Arguments</th><th>Exit</th><th>Duration</th></tr>");
            foreach (var b in bats)
                sb.Append($"<tr><td dir=\"auto\"><code>{E(string.Join(" | ", (b?["Args"]?.AsArray() ?? new JsonArray()).Select(a => Trim(a?.ToString() ?? "", 120))))}</code></td><td>{ReportData.D(b, "ExitCode")}</td><td>{ReportData.D(b, "DurationMs"):0} ms</td></tr>");
            sb.Append("</table>");
        }
        var steps = s["Steps"]?.AsArray() ?? new JsonArray();
        if (steps.Count > 0)
            sb.Append("<h4>Steps</h4><pre>").Append(E(string.Join("\n", steps.Select(x => x?.ToString())))).Append("</pre>");
        var obs = s["Observations"]?.AsObject();
        if (obs != null && obs.Count > 0)
        {
            sb.Append("<h4>Observations</h4><table class=\"kv\">");
            foreach (var kv in obs)
                sb.Append($"<tr><th>{E(kv.Key)}</th><td dir=\"auto\"><code>{E(Trim(kv.Value?.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) ?? "", 2000))}</code></td></tr>");
            sb.Append("</table>");
        }
        var reqs = s["ProviderRequests"]?.AsArray() ?? new JsonArray();
        sb.Append($"<h4>Captured provider requests ({reqs.Count})</h4>");
        foreach (var r in reqs.Take(4))
        {
            var headers = string.Join("\n", (r?["Headers"]?.AsObject() ?? new JsonObject())
                .Where(h => h.Key is "Content-Type" or "SOAPAction" or "Authorization" or "X-Request-Source")
                .Select(h => $"{h.Key}: {(h.Key == "Authorization" ? h.Value?.ToString().Split(' ')[0] + " ***" : h.Value)}"));
            sb.Append($"<div class=\"req\"><div class=\"rh\">#{ReportData.D(r, "Seq")} {E(ReportData.S(r, "Method"))} {E(ReportData.S(r, "Path"))} → HTTP {ReportData.D(r, "ResponseStatus")} {(r?["Accepted"]?.GetValue<bool>() == true ? "accepted" : "not accepted")} {E(ReportData.S(r, "FaultApplied"))}</div>");
            sb.Append($"<pre dir=\"auto\">{E(headers)}\n\n{E(Pretty(ReportData.S(r, "Body")))}</pre>");
            sb.Append($"<pre class=\"resp\" dir=\"auto\">{E(Trim(ReportData.S(r, "ResponseBody"), 1500))}</pre>");
            var errs = r?["ContractErrors"]?.AsArray();
            if (errs is { Count: > 0 }) sb.Append($"<p class=\"bad\">Contract errors: {E(string.Join("; ", errs.Select(x => x?.ToString())))}</p>");
            var warns = r?["ContractWarnings"]?.AsArray();
            if (warns is { Count: > 0 }) sb.Append($"<p class=\"warn\">Contract warnings: {E(string.Join("; ", warns.Select(x => x?.ToString())))}</p>");
            sb.Append("</div>");
        }
        if (reqs.Count > 4) sb.Append($"<p class=\"note\">{reqs.Count - 4} more in the JSON evidence.</p>");
        sb.Append("</details>");
    }

    private static string Pretty(string body)
    {
        var t = body.TrimStart();
        try
        {
            if (t.StartsWith('<')) return XDocument.Parse(body).ToString();
            if (t.StartsWith('{')) return JsonSerializer.Serialize(JsonDocument.Parse(body).RootElement, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
        catch { }
        return Trim(body, 3000);
    }

    private static string Trim(string s, int max = 300) => s.Length <= max ? s : s[..max] + $"… ({s.Length} chars)";

    private const string Css = """
:root{--bg:#f7f7f5;--fg:#1d1d1f;--muted:#6b6b70;--card:#fff;--line:#e3e3e0;--good:#1a7f37;--bad:#c62828;--warn:#9a6700;--accent:#3557c5;--c1:#3557c5;--c2:#c2410c;--c3:#7c3aed;--c4:#0f766e}
@media (prefers-color-scheme:dark){:root{--bg:#141416;--fg:#ececee;--muted:#a0a0a8;--card:#1e1e22;--line:#34343a;--good:#4ac26b;--bad:#ff6b6b;--warn:#e3b341;--accent:#8aa4ff;--c1:#8aa4ff;--c2:#fb923c;--c3:#c4a7ff;--c4:#5eead4}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:14px/1.5 "Segoe UI",system-ui,sans-serif}
main{max-width:1200px;margin:0 auto;padding:16px}h1{font-size:24px;margin:8px 0}h2{margin-top:32px;border-bottom:1px solid var(--line);padding-bottom:4px}
.sub,.note{color:var(--muted)}nav{position:sticky;top:0;background:var(--bg);padding:8px 0;border-bottom:1px solid var(--line);display:flex;gap:16px;flex-wrap:wrap;z-index:2}
nav a{color:var(--accent);text-decoration:none}.verdict{display:inline-block;padding:8px 14px;border-radius:8px;font-weight:600;margin:8px 0}
.verdict.good{background:color-mix(in srgb,var(--good) 15%,transparent);color:var(--good)}.verdict.bad{background:color-mix(in srgb,var(--bad) 15%,transparent);color:var(--bad)}
.tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:12px;margin:12px 0}.tile{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:12px}
.tl{color:var(--muted);font-size:12px;text-transform:uppercase;letter-spacing:.04em}.tv{font-size:26px;font-weight:600}.ts{color:var(--muted);font-size:12px}
table{border-collapse:collapse;width:100%;background:var(--card);margin:8px 0}th,td{border:1px solid var(--line);padding:5px 8px;text-align:left;vertical-align:top}
th{background:color-mix(in srgb,var(--line) 40%,transparent);font-weight:600}table.kv th{width:240px}.scroll{overflow-x:auto}
code,pre{font-family:Consolas,"Cascadia Mono",monospace;font-size:12px}pre{background:var(--card);border:1px solid var(--line);padding:8px;overflow-x:auto;white-space:pre-wrap;word-break:break-word}
pre.resp{border-left:3px solid var(--accent)}.req{margin:8px 0}.rh{font-weight:600;font-size:12px}
.pill{display:inline-block;padding:1px 8px;border-radius:10px;font-size:12px;font-weight:600}.pass{background:color-mix(in srgb,var(--good) 18%,transparent);color:var(--good)}
.fail{background:color-mix(in srgb,var(--bad) 18%,transparent);color:var(--bad)}.open,.not-run{background:color-mix(in srgb,var(--warn) 18%,transparent);color:var(--warn)}
.sev-high{background:color-mix(in srgb,var(--bad) 18%,transparent);color:var(--bad)}.sev-medium{background:color-mix(in srgb,var(--warn) 18%,transparent);color:var(--warn)}.sev-low{background:color-mix(in srgb,var(--accent) 15%,transparent);color:var(--accent)}
.bad{color:var(--bad)}.warn{color:var(--warn)}details{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:8px 12px;margin:8px 0}summary{cursor:pointer}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(360px,1fr));gap:12px}.chart{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:8px}
.chart svg{width:100%;height:auto}.chart text{fill:var(--muted);font-size:11px}.chart .ttl{fill:var(--fg);font-size:12px;font-weight:600}.chart line.ax{stroke:var(--line)}
footer{margin:32px 0;color:var(--muted)}
""";
}

static class Charts
{
    private static readonly string[] SeriesColors = { "var(--c1)", "var(--c2)", "var(--c3)", "var(--c4)" };
    private static readonly string[] SeriesKeys = { "P50", "P95", "P99", "Max" };

    private static double Nice(double v)
    {
        if (v <= 0) return 1;
        var exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
        foreach (var m in new[] { 1, 2, 2.5, 5, 10 })
            if (m * exp >= v) return m * exp;
        return 10 * exp;
    }

    private static string Fmt(double ms) => ms >= 10000 ? $"{ms / 1000:0}s" : ms >= 1000 ? $"{ms / 1000:0.#}s" : $"{ms:0}ms";

    public static string PercentileBars(List<JsonObject> runs)
    {
        const int w = 1100, h = 300, left = 60, bottom = 60, top = 30;
        var max = Nice(runs.Max(r => ReportData.D(r["BatLatencyMs"], "Max") is var m && !double.IsNaN(m) ? m : 0));
        var plotW = w - left - 20;
        var plotH = h - bottom - top;
        var groupW = plotW / (double)runs.Count;
        var barW = Math.Min(28, groupW / 5);
        var sb = new StringBuilder($"<div class=\"chart\"><svg viewBox=\"0 0 {w} {h}\" role=\"img\" aria-label=\"Bat latency percentiles per run\">");
        for (var i = 0; i <= 4; i++)
        {
            var y = top + plotH - plotH * i / 4.0;
            sb.Append($"<line class=\"ax\" x1=\"{left}\" x2=\"{w - 20}\" y1=\"{y:0.#}\" y2=\"{y:0.#}\"/><text x=\"{left - 6}\" y=\"{y + 4:0.#}\" text-anchor=\"end\">{Fmt(max * i / 4)}</text>");
        }
        for (var g = 0; g < runs.Count; g++)
        {
            var x0 = left + g * groupW + (groupW - barW * 4) / 2;
            for (var k = 0; k < 4; k++)
            {
                var v = ReportData.D(runs[g]["BatLatencyMs"], SeriesKeys[k]);
                if (double.IsNaN(v)) continue;
                var bh = plotH * v / max;
                sb.Append($"<rect x=\"{x0 + k * barW:0.#}\" y=\"{top + plotH - bh:0.#}\" width=\"{barW - 2:0.#}\" height=\"{bh:0.#}\" rx=\"2\" fill=\"{SeriesColors[k]}\"><title>{ReportData.S(runs[g], "Id")} {SeriesKeys[k]}: {Fmt(v)}</title></rect>");
            }
            sb.Append($"<text x=\"{left + g * groupW + groupW / 2:0.#}\" y=\"{h - bottom + 16}\" text-anchor=\"middle\">{WebUtility.HtmlEncode(ReportData.S(runs[g], "Id"))}</text>");
        }
        for (var k = 0; k < 4; k++)
            sb.Append($"<rect x=\"{left + k * 80}\" y=\"{h - 22}\" width=\"12\" height=\"12\" rx=\"2\" fill=\"{SeriesColors[k]}\"/><text x=\"{left + k * 80 + 16}\" y=\"{h - 12}\">{SeriesKeys[k].ToLowerInvariant()}</text>");
        sb.Append("</svg></div>");
        return sb.ToString();
    }

    public static string Scatter(JsonObject run)
    {
        const int w = 520, h = 240, left = 52, bottom = 34, top = 24, right = 12;
        var lat = run["BatLatenciesMs"]?.AsArray().Select(x => x!.GetValue<double>()).ToArray() ?? [];
        var off = run["BatStartOffsetsMs"]?.AsArray().Select(x => x!.GetValue<double>()).ToArray() ?? [];
        var exits = run["BatExitCodes"]?.AsArray().Select(x => x!.GetValue<int>()).ToArray() ?? [];
        if (lat.Length == 0) return "";
        var maxY = Nice(lat.Max());
        var maxX = Nice(Math.Max(1, off.Max()));
        var pw = w - left - right;
        var ph = h - top - bottom;
        var sb = new StringBuilder($"<div class=\"chart\"><svg viewBox=\"0 0 {w} {h}\" role=\"img\" aria-label=\"Latency vs start offset\">");
        sb.Append($"<text class=\"ttl\" x=\"{left}\" y=\"14\">{WebUtility.HtmlEncode(ReportData.S(run, "Id"))} · N={lat.Length}</text>");
        for (var i = 0; i <= 4; i++)
        {
            var y = top + ph - ph * i / 4.0;
            sb.Append($"<line class=\"ax\" x1=\"{left}\" x2=\"{w - right}\" y1=\"{y:0.#}\" y2=\"{y:0.#}\"/><text x=\"{left - 4}\" y=\"{y + 4:0.#}\" text-anchor=\"end\">{Fmt(maxY * i / 4)}</text>");
            var x = left + pw * i / 4.0;
            sb.Append($"<text x=\"{x:0.#}\" y=\"{h - bottom + 14}\" text-anchor=\"middle\">{Fmt(maxX * i / 4)}</text>");
        }
        sb.Append($"<text x=\"{left + pw / 2}\" y=\"{h - 4}\" text-anchor=\"middle\">bat start offset → (y: end-to-end latency)</text>");
        for (var i = 0; i < lat.Length; i++)
        {
            var color = exits.ElementAtOrDefault(i) switch { 0 => "var(--c1)", 2 => "var(--bad)", 3 => "var(--c2)", _ => "var(--c3)" };
            sb.Append($"<circle cx=\"{left + pw * off[i] / maxX:0.#}\" cy=\"{top + ph - ph * lat[i] / maxY:0.#}\" r=\"3\" fill=\"{color}\" fill-opacity=\"0.8\"><title>exit {exits.ElementAtOrDefault(i)} · {Fmt(lat[i])}</title></circle>");
        }
        var codes = exits.Distinct().OrderBy(x => x).ToList();
        for (var k = 0; k < codes.Count; k++)
        {
            var color = codes[k] switch { 0 => "var(--c1)", 2 => "var(--bad)", 3 => "var(--c2)", _ => "var(--c3)" };
            sb.Append($"<circle cx=\"{w - right - 150 + k * 50}\" cy=\"10\" r=\"4\" fill=\"{color}\"/><text x=\"{w - right - 143 + k * 50}\" y=\"14\">exit {codes[k]}</text>");
        }
        sb.Append("</svg></div>");
        return sb.ToString();
    }
}

static class MarkdownReport
{
    public static string Render(ReportData d)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# SCADA SMS — mock integration suite summary").AppendLine();
        sb.AppendLine($"Generated {ReportData.S(d.Env, "generatedAt")} · commit `{ReportData.S(d.Env, "gitSha")}` · verdict: **{Analysis.Verdict(d)}**").AppendLine();
        if (d.Counts.Count > 0)
        {
            sb.AppendLine("| Test run | Total | Passed | Failed | Skipped |").AppendLine("|---|---|---|---|---|");
            foreach (var c in d.Counts) sb.AppendLine($"| {c.Name} | {c.Total} | {c.Passed} | {c.Failed} | {c.Skipped} |");
            sb.AppendLine();
        }
        sb.AppendLine("## Scenarios").AppendLine().AppendLine("| Scenario | REST | SOAP |").AppendLine("|---|---|---|");
        foreach (var id in d.Scenarios.Select(s => ReportData.S(s, "Id")).Distinct().OrderBy(x => x, StringComparer.Ordinal))
        {
            string St(string kind) => d.Scenarios.FirstOrDefault(s => ReportData.S(s, "Id") == id && ReportData.S(s, "Kind") == kind) is { } s ? (!ReportData.Passed(s) ? "FAIL" : (s["Findings"]?.AsArray().Count ?? 0) > 0 ? "FINDING " + string.Join(", ", s["Findings"]!.AsArray().Select(f => ReportData.S(f, "Id"))) : "PASS") : "n/a";
            var title = ReportData.S(d.Scenarios.First(s => ReportData.S(s, "Id") == id), "Title");
            sb.AppendLine($"| {id} {title} | {St("REST")} | {St("SOAP")} |");
        }
        sb.AppendLine().AppendLine("## Load (alarm storm)").AppendLine();
        sb.AppendLine("Latency percentiles are nearest-rank. A zero-loss run with duplicate SMS is DEGRADED (not FAIL).").AppendLine();
        sb.AppendLine("| Run | N | Exit codes | p50 | p95 | p99 | max | Alarms/s | SMS/s | Full delivery | Missing | Zero-loss | Dup | No-dup | Status |").AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var l in d.Loads.OrderBy(x => ReportData.S(x, "Kind")).ThenBy(x => ReportData.S(x, "Id"), StringComparer.Ordinal))
        {
            var lat = l["BatLatencyMs"];
            var full = ReportData.D(l, "SecondsToFullDelivery");
            sb.AppendLine($"| {ReportData.S(l, "Kind")} {ReportData.S(l, "Id")} | {ReportData.D(l, "Burst")} | {Analysis.ExitCodes(l)} | {Analysis.Ms(lat, "P50")} | {Analysis.Ms(lat, "P95")} | {Analysis.Ms(lat, "P99")} | {Analysis.Ms(lat, "Max")} | " +
                          $"{ReportData.D(l, "AlarmsPerSecond"):0.0} | {ReportData.D(l, "SmsPerSecondDuringBurst"):0.0} | {(full < 0 ? "not reached" : $"{full:0.0} s")} | {ReportData.D(l, "MissingPairs")} | {(ReportData.Passed(l) ? "yes" : "NO")} | {ReportData.D(l, "DuplicateAcceptedSms")} | {(ReportData.D(l, "DuplicateAcceptedSms") > 0 ? "no" : "yes")} | {Analysis.LoadStatus(l)} |");
        }
        sb.AppendLine().AppendLine("## Findings").AppendLine();
        foreach (var f in d.Findings) sb.AppendLine($"- **{f.Severity} · {f.Id} · {f.Title}.** {f.Detail}");
        sb.AppendLine().AppendLine("## Go/no-go checklist").AppendLine().AppendLine("| Area | Item | Status |").AppendLine("|---|---|---|");
        foreach (var c in d.Checklist) sb.AppendLine($"| {c.Area} | {c.Item} | {c.Status} |");
        sb.AppendLine().AppendLine("No SMS was sent to any real phone or provider: every request went to the loopback mock.");
        return sb.ToString();
    }
}
