# SCADA SMS System — Codebase Context

## Purpose

This repository contains a Windows-oriented SCADA SMS notification system. WinCC OA or another SCADA client posts an alarm message and target group to the application; the application resolves eligible group members, sends one SMS per recipient through a configurable REST or SOAP provider, and records delivery attempts for auditing. It also provides a Razor Pages administration UI for users, groups, alarm mappings, holidays, provider settings, testing, and audit history.

## Technology and deployment

- .NET 9 ASP.NET Core web application (`SCADASMSSystem.Web.csproj`)
- Razor Pages UI plus attribute-routed API controllers
- Entity Framework Core 9 with SQL Server, SQL Express, or LocalDB
- Serilog console and rolling-file logging
- Bootstrap 5 and jQuery in the browser
- Hosted background service for queued SMS delivery
- Optional Windows Service hosting
- WiX v4 installer and self-contained `win-x64` release tooling
- Playwright end-to-end tests (currently focused on SOAP/configuration behavior)

The solution has one main web project and a separate WiX installer project under `Installer/`.

## Runtime architecture

The main delivery path is:

1. SCADA invokes `Scada_sms.bat` or calls `POST /api/sms/send`.
2. `Controllers/SmsController.cs` validates the payload and calls `SmsBackgroundService.QueueSmsMessageAsync`.
3. `Services/SmsBackgroundService.cs` places the request in an in-memory `ConcurrentQueue` and processes it in a hosted background loop.
4. `Services/SmsService.cs` loads eligible recipients through `IUserService`, applies rate limiting and duplicate suppression, resolves dynamic provider parameters, and sends REST or SOAP requests (or uses the in-process mock in test mode).
5. Each per-recipient attempt is written to the SMS audit data through `IAuditService`.

The accepted SCADA payload is:

```json
{
  "message": "Equipment failure detected",
  "groupId": 1,
  "value": "85.3 PSI",
  "alarmId": "ALM-2024-0142",
  "priority": "urgent"
}
```

`message` and a positive `groupId` are required. `priority`, when supplied, is `normal`, `urgent`, or `critical`. `value` and `alarmId` are optional. Compatibility routes also accept `POST /api/sms/queue`, `POST /api/sms`, and root-level `POST /`. Status endpoints include `/api/sms/status`, `/api/sms/health`, root `/status`, and the ASP.NET health endpoint `/health`.

The queue is process-local and not durable: an application restart loses queued but unprocessed items. Rate-limit and deduplication state are also in memory.

## Critical integration entry point: `Scada_sms.bat`

`Scada_sms.bat` is the production-facing bridge between the SCADA alarm action and the ASP.NET application. It does not send an SMS directly. It converts positional command-line arguments into JSON and performs a local HTTP POST to the application's queue endpoint.

### Invocation contract

```bat
Scada_sms.bat MESSAGE GROUP_ID [VALUE] [ALARM_ID] [PRIORITY]
```

Examples:

```bat
Scada_sms.bat "High temperature alarm" 3
Scada_sms.bat "Pump pressure" 7 "185.2 bar"
Scada_sms.bat "Fire alarm zone 4" 2 "" "ALM-0091" "critical"
Scada_sms.bat "תקלה במשאבה" 3
```

The positional order is significant:

| Position | Batch variable | JSON field | Behavior |
| --- | --- | --- | --- |
| `%1` | `MSG` | `Message` | Required alarm text |
| `%2` | `GRP` | `GroupId` | Required; checked as digits before the request |
| `%3` | `VALUE` | `Value` | Optional measurement/tag value; omitted from JSON when empty |
| `%4` | `ALM` | `AlarmId` | Optional; defaults to `SCADA-%RANDOM%` |
| `%5` | `PRI` | `Priority` | Optional; defaults to `normal` |

`%~N` removes the caller's surrounding quotes. Therefore message/value arguments containing spaces must be quoted by the SCADA invocation. To omit `VALUE` while supplying later parameters, pass an explicit empty third argument (`""`).

### What the script does

1. Switches the Windows console to UTF-8 with `chcp 65001` and enables delayed expansion.
2. Exits with code `1` if message or group ID is absent.
3. Reads the five positional arguments and supplies default alarm ID and priority values.
4. Uses `findstr` to reject a group ID containing non-digit characters.
5. Escapes double quotes in `MSG` and `VALUE` for JSON.
6. Selects curl from `..\Tools\curl.exe` relative to the script directory, falling back to `curl.exe` on `PATH`.
7. Writes a temporary UTF-8 JSON payload under `%TEMP%`. It uses `--data-binary` so curl sends the file contents without form conversion.
8. Posts the payload to `http://localhost:5000/api/sms/send` with JSON content type, a 5-second connection timeout, and a 15-second total timeout.
9. Captures curl's process exit code, deletes the temporary JSON file, and returns that code to SCADA.

The source file must remain **UTF-8 with BOM and CRLF line endings** for its Hebrew/Unicode behavior in Windows `cmd.exe`. The current repository copy has both properties. Do not casually normalize it to UTF-8 without BOM or LF-only endings.

### Exit behavior and observability

- `0` means curl completed successfully at the transport level.
- `1` is used for missing required arguments or an invalid group ID.
- Other nonzero values are curl exit codes (for example, connection or timeout failures).
- The script uses curl `--silent` and redirects stdout/stderr to `nul`, so it produces no response body or diagnostic output.
- Curl is not invoked with `--fail`; consequently, an HTTP `400`, `500`, or `503` can still produce curl exit code `0`. The exit status proves that the HTTP exchange completed, not that the application accepted or delivered the SMS.
- A successful API response means the message was queued, not delivered. Actual provider delivery and audit results happen asynchronously in `SmsBackgroundService`/`SmsService`.
- The generated default alarm ID uses `%RANDOM%` and is not globally unique. SCADA should pass a stable correlation ID when audit correlation matters.

### Escaping limitations

Only embedded double quotes are explicitly JSON-escaped, and only for message/value. Backslashes, control characters, percent expansion, delayed-expansion exclamation marks, and special `cmd.exe` metacharacters are not comprehensively protected. `ALM` and `PRI` are inserted into JSON without JSON escaping. Treat SCADA-provided arguments as controlled values and test any change involving quotes, `!`, `%`, `&`, `|`, `<`, `>`, backslashes, or line breaks on Windows `cmd.exe`.

### Source and packaged-copy warning

The root `Scada_sms.bat` is the current source-of-truth implementation for the five-field payload and Unicode/temp-file handling. However, the legacy `build.bat` currently *generates* `deployment_package\Scripts\Scada_sms.bat` from an older inline template. That generated version appends `VALUE` to the message instead of sending a `Value` field, constructs JSON directly on the curl command line, and lacks the root script's Unicode/temp-file behavior. A package produced by `build.bat` may therefore not match this documented source script.

`build-release.ps1` publishes and harvests application output for the MSI but does not visibly stage the root batch script in its release steps. Before shipping, explicitly verify which `Scada_sms.bat` is included in the deployment/MSI and compare it with the root file. Do not assume that editing the root script automatically updates every deployment artifact.

### Safe validation checklist

- Run on Windows `cmd.exe`, not only from WSL or PowerShell.
- Confirm the web application is listening on `localhost:5000`.
- Exercise minimal, value-bearing, full five-argument, Hebrew, embedded-quote, and unavailable-server cases.
- Verify the request in the API/background-service logs and the resulting audit row; the batch file itself suppresses the server response.
- Test both curl layouts: bundled `Scripts\Scada_sms.bat` + `Tools\curl.exe`, and system curl fallback.
- Recheck the resulting deployment artifact after running either release pipeline.

## Startup and dependency wiring

`Program.cs` is the composition root. It:

- changes the current directory to the executable directory for Windows Service compatibility;
- loads `appsettings.json`, the environment-specific settings file, and environment variables;
- configures Serilog and Windows Event Log integration;
- registers Razor Pages, controllers, EF Core, HTTP clients, application services, mock/debug logs, the hosted SMS service, and health checks;
- applies EF migrations and runs seed initialization at startup;
- maps static assets, Razor Pages, API controllers, and `/health`.

Migration failure stops startup in Development but is logged and tolerated in Production. There is a bootstrap shim for databases created before EF migration history was introduced.

## Important directories and files

| Location | Responsibility |
| --- | --- |
| `Program.cs` | Startup, dependency injection, database migration, middleware, endpoint mapping |
| `Controllers/SmsController.cs` | Public SCADA queue/status/test API and legacy-compatible routes |
| `Controllers/MockSmsController.cs` | Test-mode REST/SOAP provider simulator |
| `Services/SmsBackgroundService.cs` | In-memory queue, hosted processing loop, service counters |
| `Services/SmsService.cs` | Recipient delivery, REST/SOAP construction, template variables, retries/rate limits/deduplication |
| `Services/IServices.cs` | Core service interfaces and SMS DTOs |
| `Services/UserService.cs` | User CRUD and eligible group-recipient lookup |
| `Services/GroupService.cs` | Group and membership management |
| `Services/HolidayService.cs` | Holiday/date lookup and calendar initialization |
| `Services/AuditService.cs` | SMS audit writes, search, statistics, and retention cleanup |
| `Services/AlarmActionService.cs` | WinCC OA alarm/action parsing and group assignment updates |
| `Data/SCADADbContext.cs` | EF model, table mappings, relationships, indexes, defaults |
| `Models/` | EF entities and `SmsSettings` configuration model |
| `Migrations/` | EF schema evolution, including SCADA-prefixed table renames |
| `Pages/` | Razor Pages UI and page handlers |
| `Pages/Settings/` | Runtime provider configuration and connection testing |
| `Pages/Test/` | SMS test UI and SOAP workbench |
| `Pages/Debug/` | Diagnostics for calls, SOAP XML, groups, alarms, and audits |
| `Installer/` | WiX installer project, fragments, and harvesting scripts |
| `Scada_sms.bat` | WinCC OA-to-HTTP integration script |
| `build-release.ps1` | Versioned publish/stage/harvest/MSI pipeline |
| `build.bat` | Legacy/full Windows deployment packaging pipeline |
| `tests/` | Playwright browser tests |

## Data model

`SCADADbContext` exposes these main sets:

- `Users`: operators and their phone/SMS/holiday preferences.
- `Groups`: notification groups.
- `GroupMembers`: many-to-many user/group membership.
- `SmsAudits`: immutable-ish per-recipient delivery history; user deletion is restricted to preserve audits.
- `DateDimensions`: calendar and Jewish/sabbatical holiday flags.
- `AlarmActions`: mapping to the WinCC OA `AlarmConditions` schema.
- `DBListBlocks`: WinCC OA block-name lookup data.
- `AlarmActionAudits`: local audit history for alarm-group mapping changes.

Database table/column names are explicitly mapped and include legacy/SCADA-prefixed naming. Do not infer physical names solely from C# entity names; check `SCADADbContext.cs` and the latest migration.

## UI map

- `/` — dashboard and recent activity
- `/Users` — users and SMS/holiday toggles
- `/Groups` — groups and member assignment
- `/Alarms/ManageGroups` — alarm-to-group mapping
- `/Audit` — searchable delivery history and cleanup
- `/Calendar` — holiday calendar
- `/Settings` — provider/runtime settings and connectivity checks
- `/Test/Sms` — end-to-end SMS test and SOAP workbench
- `/Debug/*` — operational diagnostics
- `/Help/Integration` — integration guidance

This is a Razor Pages application, not MVC views or Blazor. Page behavior normally lives in a paired `.cshtml`/`.cshtml.cs` file and calls an injected service rather than using the DbContext directly.

## Configuration

Configuration is spread across `appsettings.json` and environment-specific overrides:

- `ConnectionStrings:DefaultConnection`
- `SmsSettings` (provider type, endpoint, HTTP method/content type, REST parameters/headers, SOAP envelope/body/placeholders/auth, credentials, sender, rate limits, duplicate window, retries, timeouts, test mode, XML logging)
- `Logging` and `Serilog`
- `Security`, `HealthChecks`, `BackgroundService`, `Maintenance`, and `Performance`

`SmsSettings` is monitored with `IOptionsMonitor`, so the Settings UI can update provider behavior without restarting. Dynamic values include message, group ID, alarm ID, priority, value, phone, TZ, first/last name, credentials, and sender identity. Phone is resolved per recipient; it is not part of the incoming SCADA payload.

Treat all `appsettings*.json` files as potentially sensitive. Never paste connection strings or provider credentials into documentation, tests, logs, commits, or issue reports. Use environment overrides or local configuration for secrets.

## Common development commands

From the repository root:

```bash
dotnet restore
dotnet build --configuration Release
dotnet run
```

The normal Windows service/UI deployment expects `http://localhost:5000`. Playwright is configured for `http://localhost:5042`, so start the application on that URL before running browser tests, for example:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5042"
dotnet run
npm install
npm run test:e2e
```

To build a versioned MSI on Windows (requires .NET 9 SDK and WiX v4):

```powershell
.\build-release.ps1 -Version 2.4.4
```

EF migrations should be created from the web project. `Data/SCADADbContextFactory.cs` supplies design-time context creation; inspect its selected configuration before running migration commands.

## Working conventions

- Follow existing Razor Pages and service-layer patterns.
- Inject services through constructors and keep database operations asynchronous.
- Put business logic in `Services/`, not page models or controllers.
- Preserve legacy API routes and the SCADA batch-script contract unless a migration is explicitly planned.
- Preserve existing database/table compatibility and audit history.
- Use Bootstrap 5.3 conventions already present in the UI.
- Log operational context, but mask phone numbers and never log secrets.
- Keep REST and SOAP provider modes, test mode, and live `IOptionsMonitor` reload behavior working when changing SMS settings.
- Add focused regression coverage. There is currently no dedicated C# unit-test project; CI primarily builds the application, while Playwright covers browser behavior.

## Repository state at context creation

The working tree already contains uncommitted Settings/Test UI changes, Playwright tests and reports, screenshots, and local tool settings. These are pre-existing user changes; inspect `git status` before editing and do not overwrite or clean them. Generated folders such as `bin/`, `obj/`, `node_modules/`, `playwright-report/`, `test-results/`, deployment packages, logs, screenshots, MSI files, and ZIP files are not source-of-truth code.

## Known operational caveats

- The application is Windows-first: SQL Server/LocalDB, Windows Service support, batch scripts, and WiX packaging are central to deployment.
- Startup sets the working directory from `Environment.ProcessPath`; assumptions based on the shell's launch directory may be wrong.
- Production startup continues after migration errors, so a running process does not prove the schema is current; check logs and health explicitly.
- Provider configuration supports several formats and live updates. Changes to payload resolution should be checked in REST, SOAP, and mock/test modes.
- Debug pages and mock endpoints expose operational detail. Mock endpoints require `SmsSettings:TestMode`; review exposure before enabling test/debug functionality outside a trusted network.
- README status claims may lag active code. Treat implementation, migrations, configuration models, and tests as authoritative when they disagree with prose.
