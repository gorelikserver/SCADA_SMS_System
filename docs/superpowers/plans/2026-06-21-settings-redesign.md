# Settings Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace hardcoded SMS provider fields with dynamic add/remove key-value param lists, add a secret flag per param, and add a SOAP XML log with toggle.

**Architecture:** New `SmsParam` class holds key/value/isSecret per row. `SmsSettings` gets three `List<SmsParam>` properties (RestHeaders, RestBodyParams, SoapPlaceholders) replacing the old JSON-string fields and fixed credential fields. `SmsService` uses the new lists when non-empty, falls back to legacy fields for backward compat. `SoapXmlLog` singleton holds a 100-entry ring buffer of outbound envelopes. Settings UI uses Alpine.js row editors. A new Debug page views the SOAP log.

**Tech Stack:** ASP.NET Core 9 Razor Pages, Alpine.js (already in project), Bootstrap 5, C# 13, `System.Text.Json`, `System.Collections.Concurrent`

## Global Constraints

- Target framework: net9.0
- All new files in `SCADASMSSystem.Web` namespace
- Build must produce 0 errors (existing warnings acceptable)
- Old `appsettings.json` fields (`Username`, `Password`, `ApiParams`, `ApiHeaders`, `SoapParams`, etc.) must continue working when new lists are empty — no breaking change for existing deployments
- Never skip `antiforgery` on POST handlers
- Commit after every task

---

## File Map

| File | Action | Purpose |
|---|---|---|
| `Models/SmsSettings.cs` | Modify | Add `SmsParam` class + 3 list props + `LogSoapXml` |
| `Services/SoapXmlLog.cs` | Create | 100-entry ring buffer singleton |
| `Services/SmsService.cs` | Modify | Use new lists for REST/SOAP building; inject `SoapXmlLog`; log envelope |
| `Pages/Settings/Index.cshtml.cs` | Modify | Migration on GET; save lists on POST; update snapshot |
| `Pages/Settings/Index.cshtml` | Modify | Alpine row editors for 3 lists; remove old credential fields; add LogSoapXml toggle |
| `Pages/Test/Sms.cshtml` | Modify | Collapsible Outbound XML panel in SOAP Workbench |
| `Pages/Debug/SoapLog.cshtml` | Create | SOAP XML log viewer |
| `Pages/Debug/SoapLog.cshtml.cs` | Create | Page model: list entries, clear handler |
| `Pages/Shared/_Layout.cshtml` | Modify | Add SOAP Log entry in Debug dropdown |
| `Program.cs` | Modify | Register `SoapXmlLog` singleton |
| `appsettings.json` | Modify | Add new array keys with empty defaults |

---

## Task 1: SmsParam class + SmsSettings new properties

**Files:**
- Modify: `Models/SmsSettings.cs`
- Modify: `appsettings.json`

**Interfaces:**
- Produces: `SmsParam` record (Key, Value, IsSecret); `SmsSettings.RestHeaders`, `.RestBodyParams`, `.SoapPlaceholders`, `.LogSoapXml`

- [ ] **Step 1: Add `SmsParam` class and new properties to SmsSettings**

Open `Models/SmsSettings.cs`. Add `SmsParam` as a sibling class (after `SmsSettings`) and add four new properties to `SmsSettings`. The existing fields must remain untouched.

```csharp
// Add inside the namespace block, after SmsSettings class:
public class SmsParam
{
    public string Key    { get; set; } = string.Empty;
    public string Value  { get; set; } = string.Empty;
    public bool IsSecret { get; set; } = false;
}
```

Add to the `SmsSettings` class body (after the `TestMode` property):

```csharp
// ─── Dynamic param lists ─────────────────────────────────────────────────────
// When non-empty these replace the legacy ApiHeaders/ApiParams/SoapParams strings.
public List<SmsParam> RestHeaders      { get; set; } = new();
public List<SmsParam> RestBodyParams   { get; set; } = new();
public List<SmsParam> SoapPlaceholders { get; set; } = new();

// ─── SOAP XML logging ────────────────────────────────────────────────────────
public bool LogSoapXml { get; set; } = false;
```

- [ ] **Step 2: Add new keys to `appsettings.json`**

In `appsettings.json`, inside the `"SmsSettings"` object, add:

```json
"RestHeaders": [],
"RestBodyParams": [],
"SoapPlaceholders": [],
"LogSoapXml": false
```

- [ ] **Step 3: Verify build**

```powershell
dotnet build --no-restore -v q
```

Expected: `Build succeeded.   0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add Models/SmsSettings.cs appsettings.json
git commit -m "feat: add SmsParam class and dynamic list properties to SmsSettings"
```

---

## Task 2: SoapXmlLog singleton

**Files:**
- Create: `Services/SoapXmlLog.cs`
- Modify: `Program.cs` (line ~114, after `AddSingleton<SmsCallLog>()`)

**Interfaces:**
- Produces: `SoapXmlLog` class with `Push(string maskedPhone, string xml)`, `Entries` (IReadOnlyList<Entry>), `Clear()`

- [ ] **Step 1: Create `Services/SoapXmlLog.cs`**

```csharp
using System.Collections.Concurrent;

namespace SCADASMSSystem.Web.Services
{
    public class SoapXmlLog
    {
        public record Entry(DateTime Timestamp, string MaskedPhone, string Xml);

        private readonly ConcurrentQueue<Entry> _entries = new();
        private const int MaxEntries = 100;

        public void Push(string maskedPhone, string xml)
        {
            _entries.Enqueue(new Entry(DateTime.Now, maskedPhone, xml));
            while (_entries.Count > MaxEntries)
                _entries.TryDequeue(out _);
        }

        public IReadOnlyList<Entry> Entries => _entries.ToArray();

        public void Clear() => _entries.Clear();
    }
}
```

- [ ] **Step 2: Register in `Program.cs`**

After the line `builder.Services.AddSingleton<SmsCallLog>();` (line ~114), add:

```csharp
builder.Services.AddSingleton<SoapXmlLog>();
```

- [ ] **Step 3: Verify build**

```powershell
dotnet build --no-restore -v q
```

Expected: `Build succeeded.   0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add Services/SoapXmlLog.cs Program.cs
git commit -m "feat: add SoapXmlLog singleton (100-entry ring buffer)"
```

---

## Task 3: SmsService — use new lists + inject SoapXmlLog + log envelope

**Files:**
- Modify: `Services/SmsService.cs`

**Interfaces:**
- Consumes: `SmsParam` (from Task 1), `SoapXmlLog` (from Task 2)

This task has three sub-changes: (A) inject `SoapXmlLog`, (B) REST request building from `RestHeaders`/`RestBodyParams`, (C) SOAP placeholder resolution from `SoapPlaceholders` + XML logging.

### A — Inject SoapXmlLog

- [ ] **Step 1: Add `SoapXmlLog` field and constructor parameter**

In `SmsService.cs`, the constructor currently accepts `HttpClient`, `IOptionsMonitor<SmsSettings>`, `IServiceProvider`, `SmsCallLog`, `ILogger<SmsService>`. Add `SoapXmlLog`:

Field (add after `private readonly SmsCallLog _callLog;`):
```csharp
private readonly SoapXmlLog _soapXmlLog;
```

Constructor parameter (add after `SmsCallLog callLog`):
```csharp
SoapXmlLog soapXmlLog,
```

Constructor assignment (add after `_callLog = callLog;`):
```csharp
_soapXmlLog = soapXmlLog;
```

### B — REST request building

- [ ] **Step 2: Extract REST keyword resolver into a private helper**

The keyword switch currently lives inline in the REST body-params loop (lines ~144–207). Extract it into a private method so both the legacy path and the new-list path can call it:

Add this private method anywhere inside `SmsService` (e.g., above `CheckRateLimit`):

```csharp
private string ResolveRestKeyword(string keyword, string phoneNumber, SmsRecipient recipient, SmsCallContext? ctx)
{
    switch (keyword.ToLowerInvariant())
    {
        case "message": case "msg": case "text": case "body":
            // Caller must supply the message separately — return keyword untouched here.
            // The call sites that call this pass message through the loop variable.
            return keyword;
        case "phone": case "phonenumber": case "sendtophonenumbers": case "mobile": case "number":
            return phoneNumber;
        case "tz":
            return recipient.TZ ?? "";
        case "firstname":
            return recipient.FirstName ?? "";
        case "lastname":
            return recipient.LastName ?? "";
        case "group_id": case "groupid":
            return ctx?.GroupId?.ToString() ?? "";
        case "alarm_id": case "alarmid":
            return ctx?.AlarmId ?? "";
        case "priority":
            return ctx?.Priority ?? "";
        case "value":
            return ctx?.Value ?? "";
        case "username": case "user": case "userid":
            return Settings.Username ?? "";
        case "password": case "pass":
            return Settings.Password ?? "";
        case "sender_name": case "sendername": case "sender": case "from":
            return Settings.SenderName ?? "SCADA";
        default:
            return keyword; // literal value
    }
}
```

Note: the `message` case is intentionally a passthrough — the caller supplies the message string directly (see step 3).

- [ ] **Step 3: Add new-list REST body-params path**

Find the block starting at `// Parse API parameters from JSON` (line ~129). Add a new-list branch BEFORE the existing `JsonSerializer.Deserialize` call:

Replace this block:
```csharp
// Parse API parameters from JSON
var apiParams = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings.ApiParams);
if (apiParams == null)
{
    return new SmsApiResponse { Success = false, Message = "Invalid API parameters configuration" };
}

// Build parameter dictionary
var requestParams = new Dictionary<string, string>();
bool anyPhoneMapped = false;

foreach (var param in apiParams)
{
    string paramValue;
    switch (param.Value.ToLower())
    {
        // ... existing long switch ...
    }
    requestParams[param.Key] = paramValue;
}
```

With:
```csharp
var requestParams = new Dictionary<string, string>();
bool anyPhoneMapped = false;

if (Settings.RestBodyParams.Count > 0)
{
    // New dynamic list path
    foreach (var param in Settings.RestBodyParams)
    {
        string resolvedValue;
        var kw = param.Value.ToLowerInvariant();
        if (kw is "message" or "msg" or "text" or "body")
            resolvedValue = message;
        else
        {
            resolvedValue = ResolveRestKeyword(param.Value, phoneNumber, recipient, ctx);
        }
        if (kw is "phone" or "phonenumber" or "sendtophonenumbers" or "mobile" or "number")
            anyPhoneMapped = true;
        requestParams[param.Key] = resolvedValue;
    }
}
else
{
    // Legacy: parse ApiParams JSON string
    var apiParams = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings.ApiParams ?? "{}");
    if (apiParams == null)
        return new SmsApiResponse { Success = false, Message = "Invalid API parameters configuration" };

    foreach (var param in apiParams)
    {
        string paramValue;
        switch (param.Value.ToLower())
        {
            case "message":
            case "msg":
            case "text":
            case "body":
                paramValue = message;
                break;
            case "phone":
            case "phonenumber":
            case "sendtophonenumbers":
            case "mobile":
            case "number":
                paramValue = phoneNumber;
                anyPhoneMapped = true;
                break;
            case "tz":
                paramValue = recipient.TZ ?? "";
                break;
            case "firstname":
                paramValue = recipient.FirstName ?? "";
                break;
            case "lastname":
                paramValue = recipient.LastName ?? "";
                break;
            case "group_id":
            case "groupid":
                paramValue = ctx?.GroupId?.ToString() ?? "";
                break;
            case "alarm_id":
            case "alarmid":
                paramValue = ctx?.AlarmId ?? "";
                break;
            case "priority":
                paramValue = ctx?.Priority ?? "";
                break;
            case "value":
                paramValue = ctx?.Value ?? "";
                break;
            case "username":
            case "user":
            case "userid":
                paramValue = Settings.Username ?? "";
                break;
            case "password":
            case "pass":
                paramValue = Settings.Password ?? "";
                break;
            case "sender_name":
            case "sendername":
            case "sender":
            case "from":
                paramValue = Settings.SenderName ?? "SCADA";
                break;
            default:
                paramValue = param.Value;
                break;
        }
        requestParams[param.Key] = paramValue;
    }
}
```

- [ ] **Step 4: Add new-list REST headers path**

Find the block `// Parse custom request headers` (line ~216). Add a new-list branch:

Replace:
```csharp
// Parse custom request headers
var customHeaders = new Dictionary<string, string>();
if (!string.IsNullOrEmpty(Settings.ApiHeaders))
{
    try
    {
        customHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings.ApiHeaders)
                        ?? new Dictionary<string, string>();
    }
    catch (JsonException)
    {
        _logger.LogWarning("ApiHeaders is not valid JSON — custom headers ignored");
    }
}
```

With:
```csharp
var customHeaders = new Dictionary<string, string>();
if (Settings.RestHeaders.Count > 0)
{
    foreach (var h in Settings.RestHeaders)
        if (!string.IsNullOrWhiteSpace(h.Key))
            customHeaders[h.Key] = h.Value;
}
else if (!string.IsNullOrEmpty(Settings.ApiHeaders))
{
    try
    {
        customHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings.ApiHeaders)
                        ?? new Dictionary<string, string>();
    }
    catch (JsonException)
    {
        _logger.LogWarning("ApiHeaders is not valid JSON — custom headers ignored");
    }
}
```

### C — SOAP placeholder resolution + XML logging

- [ ] **Step 5: Add new-list SOAP placeholder branch**

Find the block `if (!string.IsNullOrWhiteSpace(Settings.SoapParams))` (line ~537).

The full current block is:
```csharp
if (!string.IsNullOrWhiteSpace(Settings.SoapParams))
{
    var soapParamMap = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings.SoapParams)
                       ?? new Dictionary<string, string>();
    var phoneKws = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "phone", "phonenumber", "mobile" };
    if (!soapParamMap.Values.Any(v => phoneKws.Contains(v ?? "")))
    {
        _logger.LogWarning("No 'phone' placeholder mapped in SOAP SoapParams ...");
    }
    foreach (var (token, value) in soapParamMap)
        soapBodyContent = soapBodyContent.Replace($"{{{token}}}", ResolveSoapValue(value));
}
else
{
    // Legacy hardcoded replacements
    soapBodyContent = soapBodyContent
        .Replace("{Message}",       SecurityElement(message))
        .Replace("{Phone}",         SecurityElement(phone))
        .Replace("{Username}",      SecurityElement(Settings.Username ?? ""))
        .Replace("{Password}",      SecurityElement(Settings.Password ?? ""))
        .Replace("{SenderName}",    SecurityElement(Settings.SenderName ?? ""))
        .Replace("{FirstName}",     SecurityElement(firstName))
        .Replace("{LastName}",      SecurityElement(lastName))
        .Replace("{TZ}",            SecurityElement(tz))
        .Replace("{SendingSystem}", SecurityElement(Settings.SoapSendingSystem ?? "SCADA"))
        .Replace("{MessageType}",   SecurityElement(Settings.SoapMessageType ?? "SmsType1"));
}
```

Replace with:
```csharp
var phoneKws = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "phone", "phonenumber", "mobile" };

if (Settings.SoapPlaceholders.Count > 0)
{
    // New dynamic list path
    if (!Settings.SoapPlaceholders.Any(p => phoneKws.Contains(p.Value ?? "")))
    {
        _logger.LogWarning("No 'phone' placeholder mapped in SOAP SoapPlaceholders — recipient {PhoneNumber} may not receive SMS. " +
            "Add a row in Settings → SOAP → Placeholder Resolvers with value `phone`.", MaskPhoneNumber(phone));
    }
    foreach (var param in Settings.SoapPlaceholders)
        soapBodyContent = soapBodyContent.Replace($"{{{param.Key}}}", ResolveSoapValue(param.Value));
}
else if (!string.IsNullOrWhiteSpace(Settings.SoapParams))
{
    // Legacy SoapParams JSON string
    var soapParamMap = JsonSerializer.Deserialize<Dictionary<string, string>>(Settings.SoapParams)
                       ?? new Dictionary<string, string>();
    if (!soapParamMap.Values.Any(v => phoneKws.Contains(v ?? "")))
    {
        _logger.LogWarning("No 'phone' placeholder mapped in SOAP SoapParams — recipient {PhoneNumber} may not receive SMS. " +
            "Add a row in Settings → Body Parameters with the dynamic value `phone`.", MaskPhoneNumber(phone));
    }
    foreach (var (token, value) in soapParamMap)
        soapBodyContent = soapBodyContent.Replace($"{{{token}}}", ResolveSoapValue(value));
}
else
{
    // Legacy hardcoded replacements (backward-compatible)
    soapBodyContent = soapBodyContent
        .Replace("{Message}",       SecurityElement(message))
        .Replace("{Phone}",         SecurityElement(phone))
        .Replace("{Username}",      SecurityElement(Settings.Username ?? ""))
        .Replace("{Password}",      SecurityElement(Settings.Password ?? ""))
        .Replace("{SenderName}",    SecurityElement(Settings.SenderName ?? ""))
        .Replace("{FirstName}",     SecurityElement(firstName))
        .Replace("{LastName}",      SecurityElement(lastName))
        .Replace("{TZ}",            SecurityElement(tz))
        .Replace("{SendingSystem}", SecurityElement(Settings.SoapSendingSystem ?? "SCADA"))
        .Replace("{MessageType}",   SecurityElement(Settings.SoapMessageType ?? "SmsType1"));
}
```

- [ ] **Step 6: Update WS-Security to pull credentials from SoapPlaceholders**

Find the WS-Security header builder. Currently it uses `Settings.Username` and `Settings.Password` directly. The block looks like (approx lines 490–507):

```csharp
var soapHeader = authType.Equals("WSSecurity", StringComparison.OrdinalIgnoreCase)
    ? $@"  <soapenv:Header>
    <Security xmlns=""..."">
      <UsernameToken>
        <Username>{SecurityElement(Settings.Username ?? "")}</Username>
        <Password ...>{SecurityElement(Settings.Password ?? "")}</Password>
      </UsernameToken>
    </Security>
  </soapenv:Header>"
    : "  <soapenv:Header/>";
```

Replace the two `Settings.Username` / `Settings.Password` references with:

```csharp
var wsUser = Settings.SoapPlaceholders
    .FirstOrDefault(p => p.Key.Equals("Username", StringComparison.OrdinalIgnoreCase))?.Value
    ?? Settings.Username ?? "";
var wsPass = Settings.SoapPlaceholders
    .FirstOrDefault(p => p.Key.Equals("Password", StringComparison.OrdinalIgnoreCase))?.Value
    ?? Settings.Password ?? "";

var soapHeader = authType.Equals("WSSecurity", StringComparison.OrdinalIgnoreCase)
    ? $@"  <soapenv:Header>
    <Security xmlns=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"">
      <UsernameToken>
        <Username>{SecurityElement(wsUser)}</Username>
        <Password Type=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText"">{SecurityElement(wsPass)}</Password>
      </UsernameToken>
    </Security>
  </soapenv:Header>"
    : "  <soapenv:Header/>";
```

Also update the HttpBasic block (approx line 591):

```csharp
if (authType.Equals("HttpBasic", StringComparison.OrdinalIgnoreCase))
{
    var credentials = Convert.ToBase64String(
        Encoding.UTF8.GetBytes($"{wsUser}:{wsPass}"));
    request.Headers.TryAddWithoutValidation("Authorization", $"Basic {credentials}");
}
```

- [ ] **Step 7: Add SOAP XML logging after envelope is built**

Find the line `var soapEnvelope = $@"<?xml version=""1.0"" ...` (approx line 575). The envelope is fully assembled there. After the closing `""";` of the soapEnvelope assignment, add:

```csharp
if (Settings.LogSoapXml)
    _soapXmlLog.Push(MaskPhoneNumber(phone), soapEnvelope);
```

- [ ] **Step 8: Verify build**

```powershell
dotnet build --no-restore -v q
```

Expected: `Build succeeded.   0 Error(s)`

- [ ] **Step 9: Commit**

```bash
git add Services/SmsService.cs
git commit -m "feat: SmsService — use RestHeaders/RestBodyParams/SoapPlaceholders lists; add SOAP XML logging"
```

---

## Task 4: Settings page backend — migration + save new lists

**Files:**
- Modify: `Pages/Settings/Index.cshtml.cs`

**Interfaces:**
- Consumes: `SmsParam` (Task 1)
- Produces: `RestHeadersJson`, `RestBodyParamsJson`, `SoapPlaceholdersJson` bind properties; migration helper `MigrateLegacySettingsToLists()`

- [ ] **Step 1: Add bind properties for list JSON**

In `IndexModel`, after `[BindProperty] public SmsSettings SmsSettingsInput { get; set; } = new();`, add:

```csharp
[BindProperty] public string RestHeadersJson     { get; set; } = "[]";
[BindProperty] public string RestBodyParamsJson  { get; set; } = "[]";
[BindProperty] public string SoapPlaceholdersJson{ get; set; } = "[]";
```

- [ ] **Step 2: Add migration helper**

Add this private method to `IndexModel`:

```csharp
private void MigrateLegacySettingsToLists()
{
    var s = _smsSettings.CurrentValue;

    if (s.RestBodyParams.Count == 0 && !string.IsNullOrWhiteSpace(s.ApiParams))
    {
        try
        {
            var old = JsonSerializer.Deserialize<Dictionary<string, string>>(s.ApiParams) ?? new();
            SmsSettingsInput.RestBodyParams = old
                .Select(kvp => new SmsParam { Key = kvp.Key, Value = kvp.Value })
                .ToList();
            _logger.LogInformation("Auto-migrated {Count} REST body params from ApiParams", old.Count);
        }
        catch { /* bad JSON — skip */ }
    }

    if (s.RestHeaders.Count == 0 && !string.IsNullOrWhiteSpace(s.ApiHeaders))
    {
        try
        {
            var old = JsonSerializer.Deserialize<Dictionary<string, string>>(s.ApiHeaders) ?? new();
            SmsSettingsInput.RestHeaders = old
                .Select(kvp => new SmsParam { Key = kvp.Key, Value = kvp.Value })
                .ToList();
            _logger.LogInformation("Auto-migrated {Count} REST headers from ApiHeaders", old.Count);
        }
        catch { /* bad JSON — skip */ }
    }

    if (s.SoapPlaceholders.Count == 0)
    {
        var placeholders = new List<SmsParam>();
        if (!string.IsNullOrWhiteSpace(s.SoapParams))
        {
            try
            {
                var old = JsonSerializer.Deserialize<Dictionary<string, string>>(s.SoapParams) ?? new();
                placeholders.AddRange(old.Select(kvp => new SmsParam { Key = kvp.Key, Value = kvp.Value }));
            }
            catch { /* bad JSON */ }
        }
        // Promote legacy credential fields into placeholder rows
        bool hasUser = placeholders.Any(p => p.Key.Equals("Username", StringComparison.OrdinalIgnoreCase));
        bool hasPass = placeholders.Any(p => p.Key.Equals("Password", StringComparison.OrdinalIgnoreCase));
        if (!hasUser && !string.IsNullOrWhiteSpace(s.Username))
            placeholders.Add(new SmsParam { Key = "Username", Value = s.Username, IsSecret = false });
        if (!hasPass && !string.IsNullOrWhiteSpace(s.Password))
            placeholders.Add(new SmsParam { Key = "Password", Value = s.Password, IsSecret = true });
        if (!string.IsNullOrWhiteSpace(s.SenderName))
            placeholders.Add(new SmsParam { Key = "SenderName", Value = s.SenderName });
        if (!string.IsNullOrWhiteSpace(s.SoapSendingSystem) && s.SoapSendingSystem != "SCADA")
            placeholders.Add(new SmsParam { Key = "SendingSystem", Value = s.SoapSendingSystem });
        if (!string.IsNullOrWhiteSpace(s.SoapMessageType) && s.SoapMessageType != "SmsType1")
            placeholders.Add(new SmsParam { Key = "MessageType", Value = s.SoapMessageType });

        SmsSettingsInput.SoapPlaceholders = placeholders;
        if (placeholders.Any())
            _logger.LogInformation("Auto-migrated {Count} SOAP placeholders from legacy fields", placeholders.Count);
    }

    // Sync the JSON bind properties so the row editor UI picks them up on GET
    RestHeadersJson      = JsonSerializer.Serialize(SmsSettingsInput.RestHeaders);
    RestBodyParamsJson   = JsonSerializer.Serialize(SmsSettingsInput.RestBodyParams);
    SoapPlaceholdersJson = JsonSerializer.Serialize(SmsSettingsInput.SoapPlaceholders);
}
```

- [ ] **Step 3: Call migration helper from `OnGetAsync`**

In `OnGetAsync`, after the line `SmsSettingsInput = CurrentSmsSettings;`, add:

```csharp
MigrateLegacySettingsToLists();
```

Also initialize the JSON bind props from current settings when no migration needed:
```csharp
// Ensure bind props are initialized even when lists are already populated
if (string.IsNullOrEmpty(RestHeadersJson) || RestHeadersJson == "[]")
    RestHeadersJson = JsonSerializer.Serialize(SmsSettingsInput.RestHeaders);
if (string.IsNullOrEmpty(RestBodyParamsJson) || RestBodyParamsJson == "[]")
    RestBodyParamsJson = JsonSerializer.Serialize(SmsSettingsInput.RestBodyParams);
if (string.IsNullOrEmpty(SoapPlaceholdersJson) || SoapPlaceholdersJson == "[]")
    SoapPlaceholdersJson = JsonSerializer.Serialize(SmsSettingsInput.SoapPlaceholders);
```

- [ ] **Step 4: Update `OnPostSaveSettingsAsync` to save new list fields**

Inside `OnPostSaveSettingsAsync`, after validation and before building `smsNode`, deserialize the JSON bind properties:

```csharp
var restHeaders      = TryDeserializeParamList(RestHeadersJson);
var restBodyParams   = TryDeserializeParamList(RestBodyParamsJson);
var soapPlaceholders = TryDeserializeParamList(SoapPlaceholdersJson);
```

Add the helper method to `IndexModel`:
```csharp
private static List<SmsParam> TryDeserializeParamList(string json)
{
    try { return JsonSerializer.Deserialize<List<SmsParam>>(json) ?? new(); }
    catch { return new(); }
}
```

In the `smsNode` object initializer, add after `["TestMode"]`:
```csharp
["RestHeaders"]      = JsonNode.Parse(JsonSerializer.Serialize(restHeaders)),
["RestBodyParams"]   = JsonNode.Parse(JsonSerializer.Serialize(restBodyParams)),
["SoapPlaceholders"] = JsonNode.Parse(JsonSerializer.Serialize(soapPlaceholders)),
["LogSoapXml"]       = SmsSettingsInput.LogSoapXml,
```

- [ ] **Step 5: Update `OnGetSnapshot` to return new list fields**

In the `OnGetSnapshot` handler, the anonymous object currently returns old fields. Add the new fields and mask secret values:

```csharp
restHeaders      = s.RestHeaders.Select(p => new { p.Key, Value = p.IsSecret ? "***" : p.Value, p.IsSecret }),
restBodyParams   = s.RestBodyParams.Select(p => new { p.Key, Value = p.IsSecret ? "***" : p.Value, p.IsSecret }),
soapPlaceholders = s.SoapPlaceholders.Select(p => new { p.Key, Value = p.IsSecret ? "***" : p.Value, p.IsSecret }),
logSoapXml       = s.LogSoapXml,
```

- [ ] **Step 6: Verify build**

```powershell
dotnet build --no-restore -v q
```

Expected: `Build succeeded.   0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add Pages/Settings/Index.cshtml.cs
git commit -m "feat: Settings — auto-migrate legacy fields to dynamic lists; save/load RestHeaders/RestBodyParams/SoapPlaceholders"
```

---

## Task 5: Settings page UI — row editor

**Files:**
- Modify: `Pages/Settings/Index.cshtml`

The row editor is an Alpine.js component. Each list (RestHeaders, RestBodyParams, SoapPlaceholders) gets its own `x-data` div wrapping the editor. The form posts each list as a JSON string via a hidden input.

- [ ] **Step 1: Add hidden JSON inputs to the form**

Inside the main `<form>` tag (search for `asp-page-handler="SaveSettings"`), add three hidden inputs that Alpine.js will populate before submit:

```html
<input type="hidden" name="RestHeadersJson"      id="restHeadersJsonInput" />
<input type="hidden" name="RestBodyParamsJson"    id="restBodyParamsJsonInput" />
<input type="hidden" name="SoapPlaceholdersJson"  id="soapPlaceholdersJsonInput" />
```

Add to the form's `@submit` handler (or add a form `x-data` wrapper that serializes all three on submit). The cleanest approach: add a parent `x-data` on the form or use individual `@submit.prevent` + AJAX. Since this app uses standard POST, use a button `@click` to populate the hidden inputs before submit:

Replace the Save Settings submit button with:
```html
<button type="submit" class="btn btn-primary"
        @click="
            document.getElementById('restHeadersJsonInput').value     = JSON.stringify(window._restHeaders || []);
            document.getElementById('restBodyParamsJsonInput').value   = JSON.stringify(window._restBodyParams || []);
            document.getElementById('soapPlaceholdersJsonInput').value = JSON.stringify(window._soapPlaceholders || []);
        ">
    <i class="fas fa-save me-1"></i>Save Settings
</button>
```

Each row editor component stores its array in the matching `window._*` variable (see Step 2).

- [ ] **Step 2: Add REST Headers row editor**

In the REST tab section, replace the old `ApiHeaders` textarea section with:

```html
<!-- HTTP Headers -->
<div class="mb-3"
     x-data="{
         rows: [],
         init() {
             try { this.rows = JSON.parse('@Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.SmsSettingsInput.RestHeaders).Replace("'", "\\'"))') || []; }
             catch { this.rows = []; }
             window._restHeaders = this.rows;
             this.$watch('rows', v => window._restHeaders = v);
         },
         add() { this.rows.push({ Key: '', Value: '', IsSecret: false }); },
         remove(i) { this.rows.splice(i, 1); }
     }">
    <label class="form-label">HTTP Headers</label>
    <div class="mb-2">
        <template x-for="(row, i) in rows" :key="i">
            <div class="d-flex gap-2 mb-2 align-items-center">
                <input class="form-control form-control-sm" placeholder="Header name (e.g. X-API-Key)" x-model="row.Key" />
                <input class="form-control form-control-sm" placeholder="Value" x-model="row.Value"
                       :type="row.IsSecret ? 'password' : 'text'" autocomplete="new-password" />
                <button type="button" class="btn btn-sm" :class="row.IsSecret ? 'btn-warning' : 'btn-outline-secondary'"
                        @click="row.IsSecret = !row.IsSecret" title="Toggle secret (masks value)">
                    <i class="fas fa-lock"></i>
                </button>
                <button type="button" class="btn btn-sm btn-outline-danger" @click="remove(i)" title="Remove">
                    <i class="fas fa-times"></i>
                </button>
            </div>
        </template>
    </div>
    <button type="button" class="btn btn-sm btn-outline-primary" @click="add()">
        <i class="fas fa-plus me-1"></i>Add header
    </button>
</div>
```

- [ ] **Step 3: Add REST Body Parameters row editor**

Below the Headers editor, replace the old `ApiParams` textarea with:

```html
<!-- Body Parameters -->
<div class="mb-3"
     x-data="{
         rows: [],
         init() {
             try { this.rows = JSON.parse('@Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.SmsSettingsInput.RestBodyParams).Replace("'", "\\'"))') || []; }
             catch { this.rows = []; }
             window._restBodyParams = this.rows;
             this.$watch('rows', v => window._restBodyParams = v);
         },
         add() { this.rows.push({ Key: '', Value: '', IsSecret: false }); },
         remove(i) { this.rows.splice(i, 1); }
     }">
    <label class="form-label">Body Parameters
        <span class="text-muted fw-normal" style="font-size:.75rem;">— Value can be a runtime keyword: <code>message</code>, <code>phone</code>, <code>tz</code>, <code>firstname</code>, <code>lastname</code>, <code>group_id</code>, <code>alarm_id</code>, <code>priority</code>, <code>value</code> — or any literal string</span>
    </label>
    <div class="mb-2">
        <template x-for="(row, i) in rows" :key="i">
            <div class="d-flex gap-2 mb-2 align-items-center">
                <input class="form-control form-control-sm" placeholder="Param name (e.g. username)" x-model="row.Key" />
                <input class="form-control form-control-sm" placeholder="Value or keyword" x-model="row.Value"
                       :type="row.IsSecret ? 'password' : 'text'" autocomplete="new-password" />
                <button type="button" class="btn btn-sm" :class="row.IsSecret ? 'btn-warning' : 'btn-outline-secondary'"
                        @click="row.IsSecret = !row.IsSecret" title="Toggle secret">
                    <i class="fas fa-lock"></i>
                </button>
                <button type="button" class="btn btn-sm btn-outline-danger" @click="remove(i)" title="Remove">
                    <i class="fas fa-times"></i>
                </button>
            </div>
        </template>
    </div>
    <button type="button" class="btn btn-sm btn-outline-primary" @click="add()">
        <i class="fas fa-plus me-1"></i>Add parameter
    </button>
</div>
```

- [ ] **Step 4: Remove old REST credential/auth UI**

Remove these fields from the REST tab (they have been replaced by the dynamic lists):
- `Username` input
- `Password` input
- `SenderName` input
- `RestAuthType` select
- `RestBearerToken` input
- `RestApiKeyName`, `RestApiKeyValue`, `RestApiKeyLocation` inputs
- `ApiParams` textarea
- `ApiHeaders` textarea

Keep: endpoint, HTTP method, content-type, success/failure patterns, timeout/retry/rate fields.

- [ ] **Step 5: Add SOAP Placeholder Resolvers row editor**

In the SOAP tab, after the `SoapAction` / `SoapAuthType` fixed fields and before the body template textarea, add:

```html
<!-- SOAP Placeholder Resolvers -->
<div class="mb-3"
     x-data="{
         rows: [],
         init() {
             try { this.rows = JSON.parse('@Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.SmsSettingsInput.SoapPlaceholders).Replace("'", "\\'"))') || []; }
             catch { this.rows = []; }
             window._soapPlaceholders = this.rows;
             this.$watch('rows', v => window._soapPlaceholders = v);
         },
         add() { this.rows.push({ Key: '', Value: '', IsSecret: false }); },
         remove(i) { this.rows.splice(i, 1); }
     }">
    <label class="form-label">Placeholder Resolvers
        <span class="text-muted fw-normal" style="font-size:.75rem;">— Key = <code>{token}</code> name in body template · Value = runtime keyword or literal · <strong>Runtime keywords:</strong> <code>message</code>, <code>phone</code>, <code>tz</code>, <code>firstname</code>, <code>lastname</code>, <code>group_id</code>, <code>alarm_id</code>, <code>priority</code>, <code>value</code></span>
    </label>
    <div class="mb-2">
        <template x-for="(row, i) in rows" :key="i">
            <div class="d-flex gap-2 mb-2 align-items-center">
                <input class="form-control form-control-sm" placeholder="Placeholder token (e.g. Username)" x-model="row.Key" />
                <input class="form-control form-control-sm" placeholder="Value or runtime keyword" x-model="row.Value"
                       :type="row.IsSecret ? 'password' : 'text'" autocomplete="new-password" />
                <button type="button" class="btn btn-sm" :class="row.IsSecret ? 'btn-warning' : 'btn-outline-secondary'"
                        @click="row.IsSecret = !row.IsSecret" title="Toggle secret (masks password in UI, sends literal value in request)">
                    <i class="fas fa-lock"></i>
                </button>
                <button type="button" class="btn btn-sm btn-outline-danger" @click="remove(i)" title="Remove">
                    <i class="fas fa-times"></i>
                </button>
            </div>
        </template>
    </div>
    <button type="button" class="btn btn-sm btn-outline-primary" @click="add()">
        <i class="fas fa-plus me-1"></i>Add placeholder
    </button>
</div>
```

- [ ] **Step 6: Remove old SOAP credential fields**

Remove from the SOAP tab:
- `Username` input (if separate from REST tab)
- `Password` input
- `SenderName` input
- `SoapSendingSystem` input
- `SoapMessageType` input
- `SoapParams` textarea

Keep: `SoapAction`, `SoapAuthType`, `SoapBodyTemplate`, `SoapEnvelopeNamespaces`, `SoapSuccessPattern`, `SoapErrorPattern`.

- [ ] **Step 7: Add `LogSoapXml` toggle**

At the bottom of the SOAP tab (after error pattern field):

```html
<hr class="section-divider" />
<div class="d-flex align-items-center gap-3 mb-3">
    <div class="form-check form-switch">
        <input class="form-check-input" type="checkbox" role="switch"
               id="logSoapXmlToggle"
               asp-for="SmsSettingsInput.LogSoapXml" />
        <label class="form-check-label fw-semibold" for="logSoapXmlToggle">
            Log SOAP XML
        </label>
    </div>
    <span class="text-muted" style="font-size:.8rem;">
        Persists outbound SOAP envelopes to a 100-entry ring buffer.
        <a href="/Debug/SoapLog" target="_blank">View SOAP Log →</a>
    </span>
</div>
```

- [ ] **Step 8: Manual smoke test**

Start the app: `dotnet run`
- Navigate to `/Settings`
- Verify REST tab shows empty Headers and Body Params row editors with "+ Add" buttons
- Add a header row, click the lock icon → value field switches to password
- Click "–" → row removed
- Switch to SOAP tab → Placeholder Resolvers editor visible, LogSoapXml toggle present
- If old `appsettings.json` has `Username` / `Password` / `ApiParams` → Settings page should show pre-populated rows from migration
- Click Save → settings saved without error

- [ ] **Step 9: Commit**

```bash
git add Pages/Settings/Index.cshtml Pages/Settings/Index.cshtml.cs
git commit -m "feat: Settings UI — Alpine.js row editors for REST headers/params and SOAP placeholders; LogSoapXml toggle"
```

---

## Task 6: SOAP Workbench — Outbound XML panel

**Files:**
- Modify: `Pages/Test/Sms.cshtml`

The `OnPostFireSoapAsync` handler already returns `echoEnvelope` in its JSON response. The SOAP Workbench Alpine component (`soapWorkbench`) just needs a new `outboundXml` state property and a panel to display it.

- [ ] **Step 1: Add `outboundXml` state to the `soapWorkbench` Alpine component**

Find the `x-data` declaration for the SOAP workbench Alpine component in `Sms.cshtml`. Add `outboundXml: ''` to the state object.

- [ ] **Step 2: Populate `outboundXml` on successful fire**

In the Alpine.js fire handler, after the line that sets `this.responseBody = result.body`, add:

```javascript
this.outboundXml = result.echoEnvelope || '';
```

Reset on new fire (at the start of the fire function):
```javascript
this.outboundXml = '';
```

- [ ] **Step 3: Add collapsible Outbound XML panel**

In the SOAP Workbench right-column HTML (response panel area), add above the response body display:

```html
<div x-show="outboundXml" class="mb-3">
    <details>
        <summary class="fw-semibold text-secondary" style="cursor:pointer;user-select:none;">
            <i class="fas fa-code me-1 text-info"></i>Outbound XML
            <span class="badge bg-secondary ms-1" style="font-size:.65rem;">sent</span>
        </summary>
        <pre class="mt-2 p-3 bg-dark text-light rounded small" style="overflow-x:auto;white-space:pre-wrap;word-break:break-all;"
             x-text="outboundXml"></pre>
    </details>
</div>
```

- [ ] **Step 4: Manual smoke test**

- Navigate to `/Test/Sms` → SOAP Workbench tab
- Configure endpoint + template + auth, click Send
- "Outbound XML" section appears above the response — click to expand → shows full SOAP envelope with credentials redacted (`***`)

- [ ] **Step 5: Commit**

```bash
git add Pages/Test/Sms.cshtml
git commit -m "feat: SOAP Workbench — add collapsible Outbound XML panel"
```

---

## Task 7: /Debug/SoapLog page + nav

**Files:**
- Create: `Pages/Debug/SoapLog.cshtml`
- Create: `Pages/Debug/SoapLog.cshtml.cs`
- Modify: `Pages/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: `SoapXmlLog` (Task 2), `IOptionsMonitor<SmsSettings>` (to read `LogSoapXml` flag)

- [ ] **Step 1: Create `Pages/Debug/SoapLog.cshtml.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Models;
using SCADASMSSystem.Web.Services;

namespace SCADASMSSystem.Web.Pages.Debug
{
    public class SoapLogModel : PageModel
    {
        private readonly SoapXmlLog _log;
        private readonly IOptionsMonitor<SmsSettings> _settings;

        public SoapLogModel(SoapXmlLog log, IOptionsMonitor<SmsSettings> settings)
        {
            _log = log;
            _settings = settings;
        }

        public IReadOnlyList<SoapXmlLog.Entry> Entries { get; private set; } = Array.Empty<SoapXmlLog.Entry>();
        public bool LoggingEnabled { get; private set; }

        public void OnGet()
        {
            Entries = _log.Entries;
            LoggingEnabled = _settings.CurrentValue.LogSoapXml;
        }

        public IActionResult OnPostClear()
        {
            _log.Clear();
            TempData["SuccessMessage"] = "SOAP XML log cleared.";
            return RedirectToPage();
        }
    }
}
```

- [ ] **Step 2: Create `Pages/Debug/SoapLog.cshtml`**

```cshtml
@page
@model SCADASMSSystem.Web.Pages.Debug.SoapLogModel
@{
    ViewData["Title"] = "SOAP XML Log";
    Layout = "~/Pages/Shared/_Layout.cshtml";
}

<div class="container-fluid py-3">
    <div class="d-flex align-items-center gap-3 mb-3">
        <h4 class="mb-0"><i class="fas fa-code me-2 text-warning"></i>SOAP XML Log</h4>
        <span class="badge bg-secondary">Last @Model.Entries.Count entries (max 100, in-memory)</span>
        <form method="post" asp-page-handler="Clear" class="ms-auto">
            <button type="submit" class="btn btn-sm btn-outline-danger">
                <i class="fas fa-trash me-1"></i>Clear Log
            </button>
        </form>
        <a href="/Debug/SoapLog" class="btn btn-sm btn-outline-secondary">
            <i class="fas fa-sync me-1"></i>Refresh
        </a>
    </div>

    @if (TempData["SuccessMessage"] != null)
    {
        <div class="alert alert-success alert-dismissible fade show py-2">
            @TempData["SuccessMessage"]
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    }

    @if (!Model.LoggingEnabled)
    {
        <div class="alert alert-warning d-flex align-items-center gap-2">
            <i class="fas fa-toggle-off"></i>
            <span>Logging disabled — enable in <a href="/Settings">Settings → SOAP → Log SOAP XML</a> to capture outbound envelopes.</span>
        </div>
    }

    @if (!Model.Entries.Any())
    {
        <div class="alert alert-info">No SOAP XML entries logged yet.</div>
    }
    else
    {
        <div class="table-responsive">
            <table class="table table-sm table-hover align-middle" style="font-size:.82rem;">
                <thead class="table-dark">
                    <tr>
                        <th style="width:160px">Time</th>
                        <th style="width:120px">Phone</th>
                        <th>Envelope</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var entry in Model.Entries.Reverse())
                    {
                        <tr>
                            <td class="text-nowrap text-muted">@entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")</td>
                            <td class="text-muted">@entry.MaskedPhone</td>
                            <td>
                                <details>
                                    <summary style="cursor:pointer;" class="text-info">View XML</summary>
                                    <pre class="mt-2 p-2 bg-dark text-light rounded small" style="overflow-x:auto;white-space:pre-wrap;word-break:break-all;max-height:400px;">@entry.Xml</pre>
                                </details>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    }
</div>
```

- [ ] **Step 3: Add to Debug nav in `_Layout.cshtml`**

Find (line ~142):
```html
<li><a class="dropdown-item" asp-page="/Debug/TestAudit"><i class="fas fa-database me-2 text-muted"></i>Test Audit</a></li>
```

Add after it:
```html
<li><a class="dropdown-item" asp-page="/Debug/SoapLog"><i class="fas fa-code me-2 text-muted"></i>SOAP XML Log</a></li>
```

- [ ] **Step 4: Verify build**

```powershell
dotnet build --no-restore -v q
```

Expected: `Build succeeded.   0 Error(s)`

- [ ] **Step 5: Manual smoke test**

- Navigate to `/Debug/SoapLog`
- If `LogSoapXml = false`: warning banner shown
- Enable `LogSoapXml` in Settings, fire a SOAP send, return to `/Debug/SoapLog` → entry appears with timestamp, masked phone, expandable XML
- Click "Clear" → entries gone, success toast shown
- Debug dropdown in nav → "SOAP XML Log" link visible

- [ ] **Step 6: Commit**

```bash
git add Pages/Debug/SoapLog.cshtml Pages/Debug/SoapLog.cshtml.cs Pages/Shared/_Layout.cshtml
git commit -m "feat: add /Debug/SoapLog page — SOAP XML ring buffer viewer with clear and nav link"
```

---

## Self-Review Checklist

**Spec coverage:**
- ✅ Dynamic key-value list for REST (headers + body params) — Tasks 3, 4, 5
- ✅ Dynamic key-value list for SOAP (placeholder resolvers) — Tasks 3, 4, 5
- ✅ Secret flag per param (masks UI, sends literal in request) — Tasks 1, 5
- ✅ Backward-compatible migration from old fields — Tasks 3, 4
- ✅ LogSoapXml toggle — Tasks 1, 4, 5
- ✅ SoapXmlLog singleton (100-entry ring buffer, ConcurrentQueue) — Task 2
- ✅ Workbench outbound XML panel (always shown per-send) — Task 6
- ✅ /Debug/SoapLog persistent log viewer — Task 7
- ✅ WS-Security picks username/password from SoapPlaceholders list — Task 3

**Placeholder scan:** No TBDs, no "implement later", all code steps have actual code.

**Type consistency:**
- `SmsParam` defined in Task 1, consumed in Tasks 3, 4, 5 — same class name throughout
- `SoapXmlLog.Entry` record defined in Task 2, referenced in Task 7 — consistent
- `TryDeserializeParamList` defined and called in Task 4 — consistent
- `window._restHeaders`, `window._restBodyParams`, `window._soapPlaceholders` — used in Step 1 and Steps 2–5 of Task 5 — consistent naming
