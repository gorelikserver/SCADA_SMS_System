# SOAP Workbench — Design Spec
Date: 2026-04-23

## Overview

Add a Postman-style interactive SOAP workbench to the Test page. Serves three purposes:
1. Author and validate SOAP envelope/params against customer API docs
2. Fire live requests against the real configured SOAP endpoint (prod validation)
3. Fire requests against the internal mock server (debug/test mode)

No new page or route. Everything lives at `/Test/Sms`.

---

## Page Structure

The Test page gains a top-level tab bar replacing the current heading area:

```
[ SMS Test ]  [ SOAP Workbench ]
```

- **SMS Test tab** — current UI, unchanged (group selector, send form, live preview, mock tester)
- **SOAP Workbench tab** — new full-width Postman-style panel
- Default active tab driven by `Model.CurrentProviderType`:
  - `SOAP` → Workbench tab opens first
  - `REST` → SMS Test tab opens first
- Both tabs always rendered; when provider is REST, Workbench tab shows an info callout: "Switch provider to SOAP in Settings to use this workbench."

---

## SOAP Workbench Layout

Two-column layout:

### Left Column (40%) — Request Builder

| Element | Detail |
|---|---|
| **Target toggle** | `[Real Endpoint]` / `[Mock Server]` button group. Switches endpoint field between saved settings URL and `/mock/sms/soap`. |
| **Endpoint URL** | Editable text input. Pre-filled from `SmsSettings.ApiEndpoint`. |
| **SOAPAction** | Editable text input. Pre-filled from `SmsSettings.SoapAction`. |
| **Auth accordion** | Collapsed by default. Shows `SoapAuthType` (WSSecurity / HttpBasic / None). WSSecurity and HttpBasic expose Username / Password fields (pre-filled, masked). |
| **Envelope editor** | Two sub-tabs: **Template** and **Raw** (see below). |
| **[Send]** | Fires the request. Shows spinner while in-flight. |
| **[Load from Settings]** | Re-fetches `/Settings?handler=Snapshot` and resets all fields. |
| **[Save to Settings]** | POSTs current workbench values to `?handler=SaveSoapConfig`. Shows toast on result. |

#### Envelope Editor — Template sub-tab
- Displays `SoapBodyTemplate` with placeholder tokens (`{Message}`, `{Phone}`, `{TZ}`, `{FirstName}`, `{LastName}`, `{Username}`, `{Password}`, `{SenderName}`, `{SendingSystem}`, `{MessageType}`) visually highlighted.
- For each token present in the template, renders a labeled input field below the preview.
- Live XML preview updates as inputs change (reuses `buildSoapPreview` logic from existing Alpine component).

#### Envelope Editor — Raw sub-tab
- Full editable `<textarea>` containing the assembled complete SOAP envelope XML.
- Auto-populated from Template tab values whenever user switches to Raw.
- Edits in Raw are sent as-is on fire; switching back to Template discards raw overrides — browser `confirm()` dialog warns before discarding.

### Right Column (60%) — Response Panel

| Element | Detail |
|---|---|
| **Status badge** | HTTP status code + text (`200 OK`, `500 Internal Server Error`, etc.) |
| **Elapsed time** | Round-trip ms |
| **Result indicator** | `SUCCESS` (green) / `FAILURE` (red) parsed via `SoapSuccessPattern` / `SoapErrorPattern` |
| **Error message** | Extracted text between `SoapErrorPattern` opening tag and its closing tag, shown when failure |
| **Raw response** | Scrollable dark code block, XML content |
| **Request echo** | Collapsible accordion showing the server-assembled XML that was sent (credentials redacted as `***`), for copy/compare |

---

## Backend

### New handler: `POST /Test/Sms?handler=FireSoap`

**Input (JSON body):**
```json
{
  "endpoint": "https://...",
  "soapAction": "http://tempuri.org/...",
  "envelope": "<soapenv:Envelope>...</soapenv:Envelope>",
  "authType": "WSSecurity | HttpBasic | None",
  "username": "...",
  "password": "...",
  "targetMock": false
}
```

**Behavior:**
- If `targetMock = true`: overrides `endpoint` to the internal mock URL (`/mock/sms/soap` resolved against the app's own base address via `IHttpClientFactory`).
- Assembles WS-Security SOAP header if `authType = WSSecurity` and header not already present in envelope.
- Fires HTTP POST with `Content-Type: text/xml; charset=utf-8` and `SOAPAction` header.
- Captures response body, status code, elapsed ms.
- Parses success/failure using `SoapSuccessPattern` / `SoapErrorPattern` from current `SmsSettings` (not from request body — patterns are server-side config).
- Extracts error message text between error tags if failure.

**Output (JSON):**
```json
{
  "statusCode": 200,
  "elapsedMs": 143,
  "body": "<soapenv:Envelope>...</soapenv:Envelope>",
  "success": true,
  "errorMessage": null
}
```

**Validation:**
- `endpoint` must be a well-formed absolute URL (rejects empty or malformed).
- `envelope` must be non-empty.
- Uses a named HttpClient `"SoapProbe"` registered in `Program.cs` with a 30-second timeout (matches `SmsSettings.TimeoutSeconds` default). Separate from the `SmsService` HttpClient to avoid shared state.

### New handler: `POST /Test/Sms?handler=SaveSoapConfig`

**Input (JSON body):**
```json
{
  "apiEndpoint": "...",
  "soapAction": "...",
  "soapBodyTemplate": "...",
  "soapParams": "...",
  "soapEnvelopeNamespaces": "...",
  "soapAuthType": "...",
  "username": "...",
  "password": "..."
}
```

**Behavior:**
- Updates SOAP-relevant fields + sets `ProviderType = "SOAP"` in `SmsSettings` via existing `IWritableOptions<SmsSettings>` pattern (same mechanism used by Settings page).
- Does not touch REST fields or rate-limit fields.
- Returns `{ success: true }` or `{ success: false, message: "..." }`.
- Frontend shows toast on result.

---

## Data Flow Summary

```
Page load
  └── Alpine reads #sms-preview-settings JSON (existing) → pre-fills workbench

User edits fields
  └── Template sub-tab: inputs → live XML preview updates
  └── Raw sub-tab: free-edit textarea

[Send] clicked
  └── POST ?handler=FireSoap → server fires SOAP → returns { statusCode, elapsedMs, body, success, errorMessage }
  └── Response panel updates

[Load from Settings]
  └── GET /Settings?handler=Snapshot → reset all workbench fields

[Save to Settings]
  └── POST ?handler=SaveSoapConfig → IWritableOptions updates appsettings.json → toast
```

---

## Not In Scope

- Request history / saved collections (can be added later)
- REST workbench equivalent (not requested)
- Auth credential vault / secrets management
- Multi-envelope / multi-step scenarios
