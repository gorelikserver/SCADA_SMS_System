# Pre-site-integration mock suite

End-to-end and load tests that behave like a real site before anyone connects to a real provider:

- the **real web app** (`SCADASMSSystem.Web.exe`, separate process, Production environment),
- a **provider profile** pointing at an in-process **mock SMS provider** (REST and SOAP),
- groups and users seeded in a dedicated SQL Server database (`scada_mock_it`),
- `Scada_sms.bat` fired through `cmd.exe` with **Akara's three arguments** (`"DESCRIPTION" GROUP "ALM_VALUE"`).

No SMS ever reaches a real phone or provider: the mock listens on `127.0.0.1` only, and a site refuses to
start if its profile URL is not the loopback mock.

## Run it

Windows, .NET 9 SDK, local SQL Server (default: the `DefaultConnection` server from `appsettings.json`).
Each test process creates its own database `scada_mock_it_<yyyyMMdd_HHmmss>_<pid>` with the app's EF
migrations and drops it when it exits; leftovers older than a day are swept. A SQL Server that is not on
this machine is refused unless `MOCK_IT_ALLOW_REMOTE_SQL=1`.

```powershell
powershell -ExecutionPolicy Bypass -File tests\integration\run-mock-suite.ps1
```

Options: `-SkipScenarios`, `-SkipLoad`, `-Configuration Release`, `-Filter "FullyQualifiedName~Soap"`,
`-SqlConnection "<connection string>"`.

The run takes about 20 minutes on a 16-thread laptop (scenarios ~11 min including the 5-minute
crash-recovery case; load ~6.5 min including the shipped-defaults run L7, ~3.5 min of it). Output goes to a timestamped folder (gitignored):

```
test-results\mock-integration\<yyyyMMdd-HHmmss>\
  report.html     offline, self-contained (inline CSS + SVG charts, no CDN) - open in any browser
  summary.md      the same results as Markdown
  env.json        machine, SDK, SQL Server version, git commit, phase durations
  scenarios\*.json, load\*.json   raw evidence (captured provider payloads, checks, DB/spool observations)
  trx\*.trx       test results per phase
  *.log           build and test console output
```

When the worktree was created from WSL, Windows git cannot resolve it; set `MOCK_IT_GIT_SHA` and
`MOCK_IT_GIT_BRANCH` so the report shows the commit.

Each site's temp folder (bat copy, `Scada_sms.ini`, spool, `settings.json`, app logs) is under
`%TEMP%\scada_mock_it\<run>\`. It is deleted after a passing test and kept (with the database) when a test
fails or `MOCK_IT_KEEP=1` is set. Each bat call is bounded by `MAXTIME` + 60 s; a call that does not exit
is killed with its whole process tree (cmd, PowerShell, curl) and fails the test.

Load results: **zero-loss** (every alarm x recipient accepted, spool empty, DB all SUCCESS) decides
pass/fail; a zero-loss run with duplicate SMS is reported as **DEGRADED**. Latency percentiles are
nearest-rank. Scenarios that document a product finding (S09, S11, S15, S16) pass by asserting current
behavior and are shown as **FINDING** in the report. The go/no-go verdict requires every mandatory case
for both REST and SOAP; a missing kind is NOT RUN and blocks GO.

## Test categories

| Category | What | Needs |
|---|---|---|
| `MockSelfTest` | The mock enforces the Sheba contract (spec samples verbatim) and every fault kind works; app-rendered profiles satisfy the contract | nothing (always runs) |
| `Scenario` | 14 end-to-end scenarios x REST and SOAP, plus SOAP-only crash recovery (S15) and missing TZ/names (S16) | `SCADA_MOCK_IT=1`, Windows, SQL Server |
| `Load` | Alarm storms of 10/50/200 concurrent bat calls, with latency/5xx/timeouts, plus the shipped default limits | `SCADA_MOCK_IT=1`, Windows, SQL Server |

Without `SCADA_MOCK_IT=1` the `Scenario` and `Load` tests are skipped, so a normal `dotnet test` of the
solution stays fast. Run one category by hand:

```powershell
$env:SCADA_MOCK_IT = '1'
dotnet test tests\SCADASMSSystem.IntegrationTests --filter "Category=Scenario"
```

## Contracts

- **SOAP**: Sheba Ensemble `SendSMS`, see [docs/integration/sheba-ensemble-soap-spec.md](../../docs/integration/sheba-ensemble-soap-spec.md).
  The mock checks POST, `Content-Type: text/xml;charset=UTF-8`, the exact `SOAPAction`, the WS-Security
  UsernameToken (PasswordText), Phone digits only, Message up to 1000 characters (Hebrew counted as
  characters), MessageType (the value Sheba assigned) and SendingSystem. It answers with the spec's success
  envelope, or `<Success>false</Success>` + `ErrorMessage` (HTTP 200). Strict Sheba mode (default on)
  also refuses an empty or missing TZ, FirstName or LastName (`SoapContract.StrictPersonFields`); scenario users
  are seeded with them, as on a correctly configured site, or a SOAP Fault on HTTP 500 for bad
  credentials. The spec samples are the fixtures in `tests/SCADASMSSystem.IntegrationTests/Fixtures`.
- **REST**: no site spec exists; a generic JSON gateway (`POST /api/v1/messages`, Bearer token,
  `X-Request-Source: scada-sms`, body `{to (E.164), text, reference = AlarmId, sender, priority, value}`,
  success = HTTP 200 and `$.status == "ok"`).

Both profiles are ordinary `ProviderProfile`s delivered through `SmsSettings:Profile` in a per-site
`settings.json` (`SettingsFile` setting) - no product code knows about the mock.

## Fault injection

`MockSmsProvider.AddFault(new FaultRule { Kind, Phone?, Times, Probability, StatusCode, LatencyMs })`:
`Latency`, `Http5xx`, `Hang` (no answer until the client times out), `Drop` (connection reset),
`SemanticFailure` (HTTP 200 + refusal in the body), `AuthFailure`. Rules can target one recipient (per-recipient
partial failure) and fire a fixed number of times or with a probability. `BaseLatencyMs` slows every call.
Every request is recorded with headers, body, parsed fields, contract errors/warnings, the answer and a
snapshot of the spool folder at the moment of the call.

## Shortened timings

To keep the run short, sites use TimeoutSeconds 3, RetryAttempts 1, bat `MAXTIME` 20 s, spool
`MinAgeSeconds` 25 s (still greater than `MAXTIME`, as on site), `PollSeconds` 5 and RateLimit 100000.
Load run **L7** uses the shipped defaults (RateLimit 10 / 60 s, TimeoutSeconds 30, RetryAttempts 3,
`MAXTIME` 120, `MinAgeSeconds` 150) to show their effect on an alarm storm.
