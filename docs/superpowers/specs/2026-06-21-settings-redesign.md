# Settings Redesign — Dynamic Params + SOAP XML Log

**Date:** 2026-06-21
**Branch:** feature/soap-integration

## Problem

Current `SmsSettings` has 30+ hardcoded fields for username, password, SOAP sending system, PCIM object ID, REST auth type, bearer token, API key fields, etc. Every deployment uses a different SMS provider with different parameters. Fields that don't apply to a deployment still clutter the UI and cannot be removed. Credentials and provider-specific values cannot be added beyond what the model pre-defines.

Additionally, there is no way to inspect the raw SOAP XML sent by the background service, making production debugging difficult.

## Scope

- Replace scattered fixed provider-specific fields with dynamic key-value param lists (REST and SOAP)
- Add secret flag per param (value masked in UI)
- Add SOAP XML log: persistent ring buffer + Debug page + per-send display in SOAP Workbench
- Backward-compatible migration from old fields on first save

## Data Model

### New record

```csharp
public class SmsParam
{
    public string Key     { get; set; } = string.Empty;
    public string Value   { get; set; } = string.Empty;
    public bool IsSecret  { get; set; } = false;
}
```

### `SmsSettings` changes

**Keep (structural fixed fields):**

| Field | Purpose |
|---|---|
| `ProviderType` | "REST" or "SOAP" selector |
| `ApiEndpoint` | Target URL |
| `HttpMethod`, `ContentType` | REST request shape |
| `SoapAction` | SOAPAction HTTP header |
| `SoapAuthType` | None / WSSecurity / HttpBasic |
| `SoapBodyTemplate` | XML template with `{Placeholder}` tokens |
| `SoapEnvelopeNamespaces` | Extra xmlns on envelope |
| `SoapSuccessPattern`, `SoapErrorPattern` | Response parsing |
| `RestSuccessPattern`, `RestFailurePattern` | Response parsing |
| `TimeoutSeconds`, `RetryAttempts` | Reliability |
| `RateLimit`, `RateWindow`, `DuplicateWindow` | Throttle / dedup |
| `TestMode` | Route to mock service |
| `LogSoapXml` | **New** — enable persistent SOAP XML log |

**Remove (replaced by dynamic lists):**
`Username`, `Password`, `SenderName`, `SoapMessageType`, `SoapSendingSystem`, `ScadaPcimObjectId`, `RestAuthType`, `RestBearerToken`, `RestApiKeyName`, `RestApiKeyValue`, `RestApiKeyLocation`, `ApiHeaders` (old JSON dict string), `ApiParams` (old string), `SoapParams` (old JSON dict string)

**Add (dynamic lists):**

```csharp
public List<SmsParam> RestHeaders    { get; set; } = new(); // HTTP request headers
public List<SmsParam> RestBodyParams { get; set; } = new(); // body key-values
public List<SmsParam> SoapPlaceholders { get; set; } = new(); // placeholder → keyword or literal
public bool LogSoapXml { get; set; } = false;
```

Serialized as JSON arrays in `appsettings.json`.

## Settings UI

### Row editor component (Alpine.js)

Each list rendered as an editable table:

```
[Key                ] [Value / ••••••     ] [🔒] [🗑]
[+ Add param]
```

- `🔒` secret toggle: switches value `<input>` to `type="password"` and sets `IsSecret=true`
- `🗑` removes row client-side
- On Save: lists serialized to hidden JSON inputs, posted to page handler

### REST tab

1. Fixed inputs: endpoint URL, HTTP method, content-type
2. Row editor: **HTTP Headers** (e.g. `Authorization: Bearer xyz`, secret=true)
3. Row editor: **Body Parameters** (e.g. `username: myuser`)
4. Fixed inputs: success pattern, failure pattern, timeout, retry, rate controls

### SOAP tab

1. Fixed inputs: endpoint URL, SOAP action, auth type (None/WSSecurity/HttpBasic)
2. Row editor: **Placeholder Resolvers**
   - Key = placeholder token name (e.g. `Username`, `SendingSystem`)
   - Value = runtime keyword or literal string
   - Available runtime keywords (shown as tooltip): `message`, `phone`, `tz`, `firstname`, `lastname`, `group_id`, `alarm_id`, `priority`, `value`
   - Secret flag for sensitive values (password, API keys)
3. Fixed inputs: body template textarea, envelope namespaces, success/error patterns
4. `LogSoapXml` toggle + link to `/Debug/SoapLog`

## SmsService Changes

### SOAP placeholder resolution

`ResolveSoapValue(keyword)` switch handles runtime keywords (`message`, `phone`, `tz`, etc.) unchanged. After the switch, fall back to `SoapPlaceholders` list lookup by key → return value.

WS-Security header:
```csharp
var wsUser = Settings.SoapPlaceholders.FirstOrDefault(p => p.Key == "Username")?.Value ?? "";
var wsPass = Settings.SoapPlaceholders.FirstOrDefault(p => p.Key == "Password")?.Value ?? "";
```

### REST request building

- `RestHeaders` list → `request.Headers.TryAddWithoutValidation(key, value)` per row
- `RestBodyParams` list → merged into form or JSON body (per `ContentType`)
- Replaces current `ApiHeaders` JSON parsing and `ApiParams` string parsing

### SOAP XML logging

After building the envelope string:
```csharp
if (Settings.LogSoapXml)
    _soapXmlLog.Push(MaskPhone(phone), envelopeXml);
_logger.LogDebug("SOAP envelope: {Xml}", envelopeXml);
```

### Migration (one-time, startup)

On startup, if all three new lists are empty and any old field is non-empty:
- Populate `SoapPlaceholders` from `Username`, `Password`, `SenderName`, `SoapMessageType`, `SoapSendingSystem`, `ScadaPcimObjectId`
- Populate `RestHeaders` from `ApiHeaders` JSON dict
- Populate `RestBodyParams` from `ApiParams` string
- Log warning: "Migrated legacy settings fields to dynamic param lists. Save Settings to persist."
- Old fields remain in `appsettings.json` but are ignored after migration

## SOAP XML Log

### `SoapXmlLog` singleton

```csharp
public class SoapXmlLog
{
    public record Entry(DateTime Timestamp, string MaskedPhone, string Xml);
    private readonly ConcurrentQueue<Entry> _entries = new();
    private const int MaxEntries = 100;

    public void Push(string maskedPhone, string xml) { ... } // evict oldest when full
    public IReadOnlyList<Entry> Entries => _entries.ToArray();
    public void Clear() => _entries.Clear();
}
```

Registered as singleton in `Program.cs`. Injected into `SmsService` and `/Debug/SoapLog` page.

### SOAP Workbench (Test page)

Add collapsible "Outbound XML" panel above response panel. Populated per-send from the SOAP fire handler response — always shown regardless of `LogSoapXml` toggle.

### `/Debug/SoapLog` page

- Table: Timestamp | Masked phone | XML (collapsed `<details>`, click to expand full envelope)
- "Clear" button → POST handler calls `SoapXmlLog.Clear()`
- Banner when `LogSoapXml == false`: "Logging disabled — enable in Settings → SOAP → Log SOAP XML"
- Linked from Debug nav and from SOAP tab in Settings

## Error Handling

- Row editor validation: key must be non-empty before save (client-side)
- `SoapBodyTemplate` still required when `ProviderType == SOAP` (existing server-side check)
- `SoapXmlLog` never throws — Push silently no-ops if `LogSoapXml == false`

## Testing

- Build passes after model changes
- Existing deployment with old `appsettings.json`: migration warning logged, settings page shows migrated rows, save persists correctly
- New deployment: empty lists, add rows, save, fire SOAP → XML appears in workbench panel and (if enabled) in `/Debug/SoapLog`
- Secret params: value masked in UI, value correctly included in SOAP envelope / REST request
