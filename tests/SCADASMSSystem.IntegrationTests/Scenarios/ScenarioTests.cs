using System.Text.RegularExpressions;
using SCADASMSSystem.IntegrationTests.Infrastructure;
using SCADASMSSystem.IntegrationTests.Mock;
using SCADASMSSystem.Web.Models;
using Xunit;

namespace SCADASMSSystem.IntegrationTests.Scenarios;

[Trait("Category", "Scenario")]
public class RestScenarios : ScenarioTests
{
    protected override string Kind => "REST";
}

[Trait("Category", "Scenario")]
public class SoapScenarios : ScenarioTests
{
    protected override string Kind => "SOAP";

    /// <summary>
    /// Service killed while the bat waits on its HTTP call. SOAP only (the real site provider), because
    /// it waits out the 5-minute stale-SENDING rule.
    /// </summary>
    [MockItFact]
    public Task S15_service_crash_mid_flight_is_recovered_from_spool() => Run(
        "S15-crash-mid-flight", "Service crash while the bat is waiting",
        "Provider answers after 4 s; the service process is killed 1.5 s after the bat fired. The bat must exit 2 and keep the spool file; " +
        "after restart the spool ingest must deliver every recipient (same AlarmId), even though the delivery rows were left in SENDING.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S15", 2);
            site.Mock.BaseLatencyMs = 4000;
            var batTask = site.RunBatAsync("Crash test alarm", group.GroupId.ToString(), "1");
            await Task.Delay(1500);
            site.StopApp();
            ev.Step("Service process killed while the bat was waiting");
            var bat = await batTask;
            ev.BatRuns.Add(bat);
            ev.Equal("bat exit code (service went away)", 2, bat.ExitCode);
            var alarmId = site.SpoolFiles().SingleOrDefault()?.Replace(".json", "");
            ev.True("spool file kept after the crash", alarmId != null, string.Join(",", site.SpoolFiles()));
            var rowsAfterCrash = await TestDb.DeliveriesForGroupAsync(group.GroupId);
            ev.Observe("deliveryRowsAfterCrash", rowsAfterCrash.Select(d => new { d.PhoneNumber, d.Status, d.Attempts }).ToList());

            site.Mock.BaseLatencyMs = 0;
            var restartedAt = DateTime.UtcNow;
            await site.StartAppAsync();
            ev.Step("Service restarted; waiting for the spool ingest (stale SENDING rows are only re-sent after 5 minutes)");
            var drained = await Site.WaitUntilAsync(() => Task.FromResult(site.SpoolFiles().Length == 0), TimeSpan.FromMinutes(8), 2000);
            var recoverySeconds = (DateTime.UtcNow - restartedAt).TotalSeconds;
            ev.Observe("secondsFromRestartToDelivered", Math.Round(recoverySeconds, 1));
            ev.True("spool drained after restart", drained, string.Join(",", site.SpoolFiles()));

            var rows = await TestDb.DeliveriesForGroupAsync(group.GroupId);
            ev.True("every recipient SUCCESS in DB", rows.Count == 2 && rows.All(r => r.Status == CriticalDeliveryStatus.Success),
                string.Join(",", rows.Select(r => r.Status)));
            foreach (var member in group.Members)
                ev.True($"provider accepted {member.Phone} at least once", site.Mock.Requests.Any(r => r.Phone == WirePhone(member.Phone) && r.Accepted));
            var duplicates = group.Members.Sum(m => Math.Max(0, site.Mock.Requests.Count(r => r.Phone == WirePhone(m.Phone) && r.Accepted) - 1));
            ev.Observe("duplicateAcceptedSms", duplicates);
            if (rowsAfterCrash.Any(r => r.Status == CriticalDeliveryStatus.Sending) && recoverySeconds > 240)
                ev.Finding("F-STALE-SENDING", "Medium", "Alarm delayed about 5 minutes after a service crash",
                    $"Rows left in SENDING by the killed process are treated as 'in progress' until CriticalDeliveryPolicy.StaleBefore (5 min). " +
                    $"Measured {recoverySeconds:0} s from restart to full delivery. {duplicates} duplicate SMS were accepted by the provider (request reached the provider before the crash).");
        }, new SiteOptions { SpoolMaxAgeMinutes = 60 });

    /// <summary>
    /// Documents product finding F-SOAP-PLACEHOLDERS: passes when the product behaves as it does today
    /// (empty person elements sent, refused by strict Sheba, alarm not delivered). Shown as FINDING in the report.
    /// </summary>
    [MockItFact]
    public Task S16_user_without_tz_or_names_is_refused_by_strict_sheba() => Run(
        "S16-missing-person-fields", "FINDING F-SOAP-PLACEHOLDERS: user without TZ/FirstName/LastName",
        "A recipient with no TZ, FirstName or LastName in the database. The Sheba spec makes these mandatory for a defined person. " +
        "Documents current product behavior: the profile renders empty <tem:TZ/>, <tem:FirstName/>, <tem:LastName/>, the strict Sheba mock refuses " +
        "(Success=false), the bat exits 3 and the alarm stays undelivered in the spool (retried until MaxAgeMinutes).",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S16", 1, withPersonFields: false);
            ev.True("mock runs in strict Sheba mode", site.Mock.Soap.StrictPersonFields);
            var alarmId = await FireAndGetAlarmIdAsync(site, ev, group, "Missing person fields", "1", 3);
            var r = site.Mock.Requests.First();
            foreach (var field in new[] { "TZ", "FirstName", "LastName" })
                ev.Equal($"<tem:{field}> sent empty", "", r.Fields[field]);
            foreach (var field in new[] { "TZ", "FirstName", "LastName" })
                ev.True($"contract error for empty {field}", r.ContractErrors.Any(e => e.StartsWith(field + " ")), string.Join("; ", r.ContractErrors));
            ev.True("refused: <Success>false</Success>, not accepted", !r.Accepted && r.ResponseBody.Contains("<Success>false</Success>"), r.ResponseBody);
            var row = (await TestDb.DeliveriesForGroupAsync(group.GroupId)).Single();
            ev.Equal("delivery row FAILED", CriticalDeliveryStatus.Failed, row.Status);
            ev.True("alarm left undelivered in the spool", site.SpoolFiles().Contains(alarmId + ".json"));
            ev.Finding("F-SOAP-PLACEHOLDERS", "Medium", "Users without TZ/FirstName/LastName get no Sheba SMS",
                "The profile sends <tem:TZ>{TZ}</tem:TZ> etc.; a user missing these renders empty elements. The Sheba spec makes them mandatory for a defined person " +
                "(or a fixed placeholder such as 'P'). Against a strict gateway the SMS is refused on every retry and the alarm is never delivered to that user. " +
                "The template language has no fallback syntax, so the only workaround today is a hard-coded placeholder for everyone. Require TZ/names for SMS users, " +
                "add a template default (e.g. {TZ|P}), or agree with Sheba that placeholders are acceptable.");
        });
}

/// <summary>
/// End-to-end scenarios acting like the real site: the real web app (separate process) with a
/// provider profile pointing at the mock, groups/users seeded in SQL Server, and Scada_sms.bat
/// fired through cmd.exe with Akara's three arguments.
/// </summary>
public abstract class ScenarioTests
{
    protected abstract string Kind { get; }

    protected const string HebrewDescription = "תקלה במשאבה 3 & לחץ גבוה! 'בדיקה' (קו ראשי)";

    protected Task Run(string id, string title, string description, Func<Site, ScenarioEvidence, Task> body,
        SiteOptions? options = null, bool startApp = true)
        => ScenarioEvidence.RunAsync(Kind, id, title, description, body, options, startApp);

    /// <summary>Phone as the provider must receive it for this profile.</summary>
    protected string WirePhone(string stored) => Kind == "SOAP" ? stored : "+972" + stored[1..];

    private static readonly Regex TimestampPrefix = new(@"^\[\d{2}/\d{2} \d{2}:\d{2}\] ");

    /// <summary>Full payload check for one accepted request against the profile and contract.</summary>
    protected void VerifyPayload(ScenarioEvidence ev, RecordedRequest r, SeededUser user, string description, string alarmId, string value)
    {
        var who = user.Phone;
        ev.True($"[{who}] no contract errors", r.ContractErrors.Count == 0, string.Join("; ", r.ContractErrors));
        ev.True($"[{who}] provider accepted", r.Accepted, r.ResponseStatus);
        ev.Equal($"[{who}] phone on the wire", WirePhone(user.Phone), r.Phone);
        ev.True($"[{who}] message = [dd/MM HH:mm] + DESCRIPTION", r.Message != null && TimestampPrefix.IsMatch(r.Message) && r.Message[14..] == description, r.Message);
        ev.True($"[{who}] credentials accepted", r.AuthOk);
        if (Kind == "SOAP")
        {
            ev.Equal($"[{who}] SOAPAction", SoapContract.SoapAction, r.Headers.GetValueOrDefault("SOAPAction"));
            ev.True($"[{who}] Content-Type text/xml UTF-8", r.Headers.GetValueOrDefault("Content-Type")?.Replace(" ", "").Equals("text/xml;charset=UTF-8", StringComparison.OrdinalIgnoreCase) == true, r.Headers.GetValueOrDefault("Content-Type"));
            ev.Equal($"[{who}] WS-Security PasswordType", SoapContract.PasswordTextType, r.Fields["PasswordType"]);
            ev.Equal($"[{who}] MessageType", "SmsType1", r.Fields["MessageType"]);
            ev.Equal($"[{who}] SendingSystem", "SCADA-SMS", r.Fields["SendingSystem"]);
            ev.Equal($"[{who}] FirstName", user.FirstName ?? "", r.Fields["FirstName"] ?? "");
            ev.Equal($"[{who}] LastName", user.LastName ?? "", r.Fields["LastName"] ?? "");
            ev.Equal($"[{who}] TZ", user.TZ ?? "", r.Fields["TZ"] ?? "");
        }
        else
        {
            ev.Equal($"[{who}] reference = AlarmId", alarmId, r.Reference);
            ev.Equal($"[{who}] value", value, r.Fields["value"]);
            ev.Equal($"[{who}] priority", "normal", r.Fields["priority"]);
            ev.Equal($"[{who}] sender", "SCADA", r.Fields["sender"]);
        }
    }

    private static string AlarmFromSpoolName(string file) => file.EndsWith(".json") ? file[..^5] : file;

    protected async Task<string> FireAndGetAlarmIdAsync(Site site, ScenarioEvidence ev, SeededGroup group, string description, string value, int expectedExit)
    {
        var bat = await site.RunBatAsync(description, group.GroupId.ToString(), value);
        ev.BatRuns.Add(bat);
        ev.Step($"Bat exited {bat.ExitCode} after {bat.DurationMs:0} ms");
        ev.Equal("bat exit code", expectedExit, bat.ExitCode);
        var alarmId = site.LastLoggedAlarmId();
        ev.True("Scada_sms.log has a line for the call", alarmId != null, string.Join(" | ", site.BatLogLines()));
        return alarmId!;
    }

    [MockItFact]
    public Task S01_happy_path_multi_recipient_hebrew() => Run(
        "S01-happy-path", "Happy path: Hebrew alarm to a 3-recipient group",
        "Akara-style call with Hebrew DESCRIPTION containing & ! ' ( ). Every recipient must get exactly one request that matches the profile/contract; " +
        "bat exits 0 only after all were accepted; spool file exists while the provider is called and is deleted afterwards; DB rows SUCCESS.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S01", 3);
            var alarmId = await FireAndGetAlarmIdAsync(site, ev, group, HebrewDescription, "85.3", 0);
            ev.Observe("alarmId", alarmId);

            var requests = site.Mock.Requests;
            ev.Equal("provider requests", 3, requests.Count);
            foreach (var member in group.Members)
            {
                var r = requests.Where(x => x.Phone == WirePhone(member.Phone)).ToList();
                ev.Equal($"[{member.Phone}] exactly one request", 1, r.Count);
                VerifyPayload(ev, r[0], member, HebrewDescription, alarmId, "85.3");
                ev.True($"[{member.Phone}] spool file present while provider was called", r[0].SpoolSnapshot?.Contains(alarmId + ".json") == true, string.Join(",", r[0].SpoolSnapshot ?? []));
            }
            ev.True("spool file deleted after exit 0", site.SpoolFiles().Length == 0, string.Join(",", site.SpoolFiles()));
            ev.True("Scada_sms.log records http=200 exit=0", site.BatLogLines().Last().Contains($"alarm={alarmId} group={group.GroupId} http=200 curl=0 exit=0"), site.BatLogLines().Last());

            var rows = await TestDb.DeliveriesForGroupAsync(group.GroupId);
            ev.Observe("deliveryRows", rows.Select(d => new { d.AlarmId, d.PhoneNumber, d.Status, d.Attempts, d.ProviderStatusCode }).ToList());
            ev.True("3 delivery rows, all SUCCESS, 1 attempt", rows.Count == 3 && rows.All(d => d.Status == CriticalDeliveryStatus.Success && d.Attempts == 1 && d.AlarmId == alarmId));
            var audits = await TestDb.AuditsForGroupAsync(group.GroupId);
            ev.True("3 audit rows SUCCESS", audits.Count == 3 && audits.All(a => a.Status == "SUCCESS"), audits.Count);

            var warnings = requests.SelectMany(r => r.ContractWarnings).Distinct().ToList();
            ev.Observe("contractWarnings", warnings);
            ev.True("no contract warnings other than Content-Type spelling", warnings.All(w => w.StartsWith("Content-Type")), string.Join("; ", warnings));
        });

    [MockItFact]
    public Task S02_partial_failure_retries_only_failed_recipient() => Run(
        "S02-partial-failure", "Partial failure: only the failed recipient is retried",
        "One of 3 recipients gets HTTP 503 on every in-request attempt. Bat must exit 3 (partial = failure) and keep the spool; " +
        "the spool ingest must later re-send ONLY that recipient, with the same AlarmId, and then delete the spool file.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S02", 3);
            var failing = group.Members[1];
            site.Mock.AddFault(new FaultRule { Kind = FaultKind.Http5xx, StatusCode = 503, Phone = WirePhone(failing.Phone), Times = 1 + site.Options.RetryAttempts });
            var alarmId = await FireAndGetAlarmIdAsync(site, ev, group, "Partial failure test", "7", 3);
            ev.True("spool file kept after partial failure", site.SpoolFiles().Contains(alarmId + ".json"), string.Join(",", site.SpoolFiles()));

            var rows = await TestDb.DeliveriesForGroupAsync(group.GroupId);
            ev.Observe("deliveryRowsAfterBat", rows.Select(d => new { d.PhoneNumber, d.Status, d.Attempts, d.ProviderStatusCode }).ToList());
            ev.True("failed recipient row FAILED with 503 after in-request retries",
                rows.Single(r => r.PhoneNumber == failing.Phone) is { Status: CriticalDeliveryStatus.Failed, ProviderStatusCode: 503 } f && f.Attempts == 1 + site.Options.RetryAttempts);
            ev.Equal("other recipients SUCCESS", 2, rows.Count(r => r.Status == CriticalDeliveryStatus.Success));

            var drained = await Site.WaitUntilAsync(() => Task.FromResult(site.SpoolFiles().Length == 0), TimeSpan.FromSeconds(site.Options.SpoolMinAgeSeconds + 40));
            ev.True("spool ingest delivered and deleted the file", drained, string.Join(",", site.SpoolFiles()));
            rows = await TestDb.DeliveriesForGroupAsync(group.GroupId);
            ev.True("all 3 rows SUCCESS with the same AlarmId", rows.Count == 3 && rows.All(r => r.Status == CriticalDeliveryStatus.Success && r.AlarmId == alarmId));

            var requests = site.Mock.Requests;
            foreach (var m in group.Members.Where(m => m != failing))
                ev.Equal($"[{m.Phone}] accepted recipient was NOT re-sent", 1, requests.Count(r => r.Phone == WirePhone(m.Phone)));
            var failedPhoneRequests = requests.Where(r => r.Phone == WirePhone(failing.Phone)).ToList();
            ev.Equal("failed recipient: in-request attempts + 1 spool retry", 2 + site.Options.RetryAttempts, failedPhoneRequests.Count);
            ev.True("failed recipient finally accepted", failedPhoneRequests.Last().Accepted);
            if (Kind == "REST")
                ev.True("retry carries the same AlarmId (reference)", failedPhoneRequests.All(r => r.Reference == alarmId));
            else
                ev.True("retry carries the same alarm text", failedPhoneRequests.All(r => r.Message?.EndsWith("Partial failure test") == true));
        });

    [MockItFact]
    public Task S03_semantic_failure_on_http_200() => Run(
        "S03-semantic-failure", Kind == "SOAP" ? "SOAP HTTP 200 with <Success>false</Success>" : "REST HTTP 200 with status=error",
        "Provider answers HTTP 200 but the body says the message was refused. HTTP 200 alone must not count as delivery: bat exits 3, spool kept, " +
        "then the spool ingest retries and delivers.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S03", 2);
            var victim = group.Members[0];
            site.Mock.AddFault(new FaultRule { Kind = FaultKind.SemanticFailure, Phone = WirePhone(victim.Phone), Times = 1 });
            var alarmId = await FireAndGetAlarmIdAsync(site, ev, group, "Semantic failure test", "1", 3);
            var first = site.Mock.Requests.First(r => r.Phone == WirePhone(victim.Phone));
            ev.Equal("provider answered HTTP 200", 200, first.ResponseStatus);
            ev.Observe("refusalBody", first.ResponseBody);
            var row = (await TestDb.DeliveriesForGroupAsync(group.GroupId)).Single(r => r.PhoneNumber == victim.Phone);
            ev.Equal("row FAILED despite HTTP 200", CriticalDeliveryStatus.Failed, row.Status);
            ev.Observe("providerMessageStored", row.ProviderMessage);
            ev.Equal("semantic refusal is not retried in-request (attempts)", 1, row.Attempts);
            ev.True("spool kept", site.SpoolFiles().Contains(alarmId + ".json"));

            var drained = await Site.WaitUntilAsync(() => Task.FromResult(site.SpoolFiles().Length == 0), TimeSpan.FromSeconds(site.Options.SpoolMinAgeSeconds + 40));
            ev.True("spool ingest delivered after the refusal", drained);
            ev.Equal("victim: refused once, accepted once", 2, site.Mock.Requests.Count(r => r.Phone == WirePhone(victim.Phone)));
            ev.Equal("other recipient sent once", 1, site.Mock.Requests.Count(r => r.Phone == WirePhone(group.Members[1].Phone)));
        });

    [MockItFact]
    public Task S04_service_down_spool_kept_then_delivered_on_start() => Run(
        "S04-service-down", "Service down: alarm spooled, delivered when the service starts",
        "The web app is not running when Akara fires. Bat must exit 2 and keep the spool file; when the service starts, SpoolIngestService " +
        "must re-import the file and deliver to every recipient.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S04", 3);
            var alarmId = await FireAndGetAlarmIdAsync(site, ev, group, HebrewDescription, "12", 2);
            ev.True("spool file kept", site.SpoolFiles().Contains(alarmId + ".json"), string.Join(",", site.SpoolFiles()));
            ev.True("Scada_sms.log shows http=000 exit=2", site.BatLogLines().Last().Contains("http=000") && site.BatLogLines().Last().Contains("exit=2"), site.BatLogLines().Last());
            ev.Equal("provider got nothing", 0, site.Mock.Requests.Count);
            var spoolJson = File.ReadAllText(Path.Combine(site.SpoolDir, alarmId + ".json"));
            ev.Observe("spoolFileContent", spoolJson);
            var spooledMessage = System.Text.Json.JsonDocument.Parse(spoolJson).RootElement.GetProperty("Message").GetString();
            ev.Equal("spool JSON keeps the Hebrew text intact", HebrewDescription, spooledMessage);

            var started = DateTime.UtcNow;
            await site.StartAppAsync();
            ev.Step("Service started");
            var drained = await Site.WaitUntilAsync(() => Task.FromResult(site.SpoolFiles().Length == 0), TimeSpan.FromSeconds(60));
            ev.Observe("secondsFromStartToDelivered", Math.Round((DateTime.UtcNow - started).TotalSeconds, 1));
            ev.True("spool drained after start", drained);
            foreach (var m in group.Members)
            {
                var r = site.Mock.Requests.Where(x => x.Phone == WirePhone(m.Phone)).ToList();
                ev.Equal($"[{m.Phone}] one request", 1, r.Count);
                VerifyPayload(ev, r[0], m, HebrewDescription, alarmId, "12");
            }
            var rows = await TestDb.DeliveriesForGroupAsync(group.GroupId);
            ev.True("all rows SUCCESS with the bat's AlarmId", rows.Count == 3 && rows.All(r => r.Status == CriticalDeliveryStatus.Success && r.AlarmId == alarmId));
        }, new SiteOptions { SpoolMinAgeSeconds = 5 }, startApp: false);

    [MockItFact]
    public Task S05_auth_failure_then_credentials_fixed() => Run(
        "S05-auth-failure", "Provider refuses the credentials, then they are fixed",
        "The provider rejects the credentials for every request (rotated password/token). Nothing may count as delivered: bat exits 3, spool kept, rows FAILED. " +
        "After the credentials are fixed, the spool ingest delivers the same alarm.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S05", 2);
            string original;
            if (Kind == "SOAP") { original = site.Mock.Soap.Password; site.Mock.Soap.Password = "rotated-by-provider"; }
            else { original = site.Mock.Rest.Token; site.Mock.Rest.Token = "rotated-by-provider"; }

            var alarmId = await FireAndGetAlarmIdAsync(site, ev, group, "Auth failure test", "0", 3);
            var rows = await TestDb.DeliveriesForGroupAsync(group.GroupId);
            ev.Observe("deliveryRowsAfterBat", rows.Select(d => new { d.PhoneNumber, d.Status, d.Attempts, d.ProviderStatusCode, d.ProviderMessage }).ToList());
            ev.True("all rows FAILED", rows.Count == 2 && rows.All(r => r.Status == CriticalDeliveryStatus.Failed));
            ev.Equal("auth refusal status", Kind == "SOAP" ? 500 : 401, rows[0].ProviderStatusCode);
            if (Kind == "SOAP" && rows[0].Attempts > 1)
                ev.Finding("F-SOAP-FAULT-RETRY", "Low", "SOAP authentication Fault (HTTP 500) is retried in-request",
                    $"A SOAP Fault for bad credentials comes back as HTTP 500, which CriticalDeliveryPolicy treats as transient: {rows[0].Attempts} attempts per recipient per pass. " +
                    "Harmless for delivery, but it multiplies failed logins against Sheba (account lockout risk) while credentials are wrong.");
            ev.True("spool kept", site.SpoolFiles().Contains(alarmId + ".json"));

            if (Kind == "SOAP") site.Mock.Soap.Password = original; else site.Mock.Rest.Token = original;
            ev.Step("Credentials fixed at the provider");
            var drained = await Site.WaitUntilAsync(() => Task.FromResult(site.SpoolFiles().Length == 0), TimeSpan.FromSeconds(site.Options.SpoolMinAgeSeconds + 40));
            ev.True("delivered after the fix", drained);
            rows = await TestDb.DeliveriesForGroupAsync(group.GroupId);
            ev.True("all rows SUCCESS", rows.All(r => r.Status == CriticalDeliveryStatus.Success));
        });

    [MockItFact]
    public Task S06_provider_timeout_is_indeterminate_and_retried() => Run(
        "S06-timeout", "Provider hangs: timeout, INDETERMINATE, retried from spool",
        "Requests to one recipient hang until the client timeout on every in-request attempt. The row must be INDETERMINATE (maybe sent), bat exits 3, " +
        "spool kept; the spool ingest re-sends it (a duplicate is preferred over a missed alarm).",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S06", 2);
            var victim = group.Members[0];
            site.Mock.AddFault(new FaultRule { Kind = FaultKind.Hang, Phone = WirePhone(victim.Phone), Times = 1 + site.Options.RetryAttempts });
            var alarmId = await FireAndGetAlarmIdAsync(site, ev, group, "Timeout test", "3", 3);
            var row = (await TestDb.DeliveriesForGroupAsync(group.GroupId)).Single(r => r.PhoneNumber == victim.Phone);
            ev.Equal("row INDETERMINATE", CriticalDeliveryStatus.Indeterminate, row.Status);
            ev.Equal("provider status 0 (no answer)", 0, row.ProviderStatusCode);
            ev.Observe("batSecondsWithTimeouts", Math.Round(ev.BatRuns[0].DurationMs / 1000, 1));
            var drained = await Site.WaitUntilAsync(() => Task.FromResult(site.SpoolFiles().Length == 0), TimeSpan.FromSeconds(site.Options.SpoolMinAgeSeconds + 40));
            ev.True("delivered from spool after the timeouts", drained);
            ev.True("victim finally accepted", site.Mock.Requests.Any(r => r.Phone == WirePhone(victim.Phone) && r.Accepted));
            ev.Equal("other recipient sent once", 1, site.Mock.Requests.Count(r => r.Phone == WirePhone(group.Members[1].Phone)));
        });

    [MockItFact]
    public Task S07_connection_drop_retried_in_request() => Run(
        "S07-connection-drop", "Connection dropped once: retried within the same call",
        "The provider resets the connection on the first attempt for one recipient. Transport loss is retryable, so the second in-request attempt delivers and the bat exits 0.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S07", 2);
            var victim = group.Members[1];
            site.Mock.AddFault(new FaultRule { Kind = FaultKind.Drop, Phone = WirePhone(victim.Phone), Times = 1 });
            await FireAndGetAlarmIdAsync(site, ev, group, "Connection drop test", "4", 0);
            var row = (await TestDb.DeliveriesForGroupAsync(group.GroupId)).Single(r => r.PhoneNumber == victim.Phone);
            ev.True("row SUCCESS after 2 attempts", row.Status == CriticalDeliveryStatus.Success && row.Attempts == 2, $"{row.Status}/{row.Attempts}");
            ev.True("spool deleted", site.SpoolFiles().Length == 0);
        });

    [MockItFact]
    public Task S08_transient_5xx_retried_in_request() => Run(
        "S08-transient-5xx", "Transient HTTP 502 once: retried within the same call",
        "First attempt for one recipient gets HTTP 502. 5xx is retryable, so the bat still exits 0 with attempts = 2.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S08", 2);
            var victim = group.Members[0];
            site.Mock.AddFault(new FaultRule { Kind = FaultKind.Http5xx, StatusCode = 502, Phone = WirePhone(victim.Phone), Times = 1 });
            await FireAndGetAlarmIdAsync(site, ev, group, "Transient 5xx test", "5", 0);
            var row = (await TestDb.DeliveriesForGroupAsync(group.GroupId)).Single(r => r.PhoneNumber == victim.Phone);
            ev.True("row SUCCESS after 2 attempts", row.Status == CriticalDeliveryStatus.Success && row.Attempts == 2, $"{row.Status}/{row.Attempts}");
        });

    [MockItFact]
    public Task S09_bad_arguments_exit_1_without_trace() => Run(
        "S09-bad-arguments", "Bad arguments from Akara",
        "Missing DESCRIPTION, missing group and non-numeric group must exit 1, write no spool file and call nobody.",
        async (site, ev) =>
        {
            foreach (var raw in new[] { "", "\"Only a message\"", "\"Pump\" abc \"1\"", "\"\" 5 \"1\"" })
            {
                var bat = await site.RunBatRawAsync(raw);
                ev.BatRuns.Add(bat);
                ev.Equal($"exit code for args [{raw}]", 1, bat.ExitCode);
            }
            ev.Equal("no spool files", 0, site.SpoolFiles().Length);
            ev.Equal("provider got nothing", 0, site.Mock.Requests.Count);
            var logLines = site.BatLogLines().Length;
            ev.Observe("batLogLinesForRejectedCalls", logLines);
            if (logLines == 0)
                ev.Finding("F-BAT-ARGS-NOLOG", "Medium", "Rejected bat calls leave no trace",
                    "Scada_sms.bat exits 1 on missing/non-numeric arguments before writing Scada_sms.log. Akara ignores exit codes, so a broken alarm action " +
                    "(e.g. empty DESCRIPTION or wrong group field) silently drops alarms with no record anywhere. Log the rejected call before exiting.");
        });

    [MockItFact]
    public Task S10_group_without_recipients() => Run(
        "S10-empty-group", "Group with no eligible recipients",
        "The group exists but has no SMS-enabled member. The service answers 502, bat exits 3 and the alarm stays in the spool (retried until MaxAgeMinutes, then spool\\failed).",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S10", 0);
            var alarmId = await FireAndGetAlarmIdAsync(site, ev, group, "Nobody to tell", "0", 3);
            ev.True("spool kept", site.SpoolFiles().Contains(alarmId + ".json"));
            ev.Equal("provider got nothing", 0, site.Mock.Requests.Count);
            ev.Equal("no delivery rows", 0, (await TestDb.DeliveriesForGroupAsync(group.GroupId)).Count);
        });

    [MockItFact]
    public Task S11_message_at_provider_length_limit() => Run(
        "S11-length-limit", "DESCRIPTION near the provider's 1000-character limit",
        "A 990-character Hebrew DESCRIPTION. The service prepends a 14-character timestamp, so the SMS text is 1004 characters and the provider refuses it. " +
        "Documents that the product neither truncates nor rejects up front: the alarm keeps failing until MaxAgeMinutes.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S11", 1);
            var description = string.Concat(Enumerable.Repeat("אזעקה ", 165)); // 990 chars
            var alarmId = await FireAndGetAlarmIdAsync(site, ev, group, description, "0", 3);
            var r = site.Mock.Requests.First();
            ev.Observe("smsLength", r.Message?.Length);
            ev.True("provider refused the over-long text", !r.Accepted && r.ContractErrors.Any(e => e.Contains("1000")), string.Join("; ", r.ContractErrors));
            ev.True("spool kept (will retry and fail again)", site.SpoolFiles().Contains(alarmId + ".json"));
            ev.Finding("F-MSG-LENGTH", "Medium", "No length guard for the 1000-character provider limit",
                $"SmsService prepends '[dd/MM HH:mm] ' (14 chars) and sends the text as is. A DESCRIPTION over 986 characters is refused by Sheba on every retry " +
                "until the spool gives up (MaxAgeMinutes) - the alarm is never delivered. Truncate (with marker) to the provider limit or make the limit a profile setting.");
        });

    [MockItFact]
    public Task S12_same_alarm_id_twice_is_idempotent() => Run(
        "S12-idempotency", "Same AlarmId fired twice (manual 4th argument)",
        "Idempotency key is AlarmId + recipient. Re-firing an alarm whose recipients were all accepted must exit 0 without any new provider request.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S12", 2);
            var alarmId = "MANUAL-" + Guid.NewGuid().ToString("N")[..12];
            var first = await site.RunBatAsync("Idempotency test", group.GroupId.ToString(), "1", alarmId);
            var second = await site.RunBatAsync("Idempotency test", group.GroupId.ToString(), "1", alarmId);
            ev.BatRuns.Add(first);
            ev.BatRuns.Add(second);
            ev.Equal("first exit", 0, first.ExitCode);
            ev.Equal("second exit", 0, second.ExitCode);
            ev.Equal("provider requests in total", 2, site.Mock.Requests.Count);
            ev.Equal("delivery rows", 2, (await TestDb.DeliveriesForGroupAsync(group.GroupId)).Count);
        });

    [MockItFact]
    public Task S13_cmd_special_characters_in_description() => Run(
        "S13-cmd-special-chars", "cmd.exe special characters in DESCRIPTION",
        "DESCRIPTION with % ^ < > | inside the quoted argument. Checks the text the provider receives against what Akara passed.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S13", 1);
            const string description = "Tank 50% full ^ level <low> | pump A&B";
            var bat = await site.RunBatAsync(description, group.GroupId.ToString(), "50%");
            ev.BatRuns.Add(bat);
            ev.Equal("bat exit code", 0, bat.ExitCode);
            var received = site.Mock.Requests.Single().Message?[14..];
            ev.Observe("sent", description);
            ev.Observe("received", received);
            ev.Equal("provider received DESCRIPTION exactly as Akara passed it", description, received);
            ev.True("alarm delivered", site.Mock.Requests.Single().Accepted);
        });

    [MockItFact]
    public Task S14_spool_written_before_provider_call_with_slow_provider() => Run(
        "S14-slow-provider", "Slow provider (2 s per SMS): bat waits for every recipient",
        "Each provider call takes 2 s. The bat must not return before the last recipient was accepted (synchronous contract) and the spool file must exist for the whole call.",
        async (site, ev) =>
        {
            var group = await TestDb.CreateGroupAsync($"{Kind}-S14", 3);
            site.Mock.BaseLatencyMs = 2000;
            var alarmId = await FireAndGetAlarmIdAsync(site, ev, group, "Slow provider test", "2", 0);
            var last = site.Mock.Requests.Max(r => r.ReceivedAtUtc.AddMilliseconds(r.DurationMs));
            ev.True("bat ended after the last provider answer", ev.BatRuns[0].EndedUtc >= last, $"bat end {ev.BatRuns[0].EndedUtc:HH:mm:ss.fff}, last answer {last:HH:mm:ss.fff}");
            ev.True("bat took at least 3 x 2 s (recipients are sent one after another)", ev.BatRuns[0].DurationMs >= 6000, ev.BatRuns[0].DurationMs);
            ev.True("spool file present at every provider call", site.Mock.Requests.All(r => r.SpoolSnapshot?.Contains(alarmId + ".json") == true));
            ev.True("spool deleted at the end", site.SpoolFiles().Length == 0);
        });
}
