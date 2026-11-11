# Sheba Ensemble SMS web service - provider contract (site spec)

Source: Sheba Medical Center (Tel HaShomer) IT division, "Characterization document - sending SMS
via Ensemble", dated 11.05.2023 (Hebrew PDF supplied by the site). This file is a faithful English
transcription of the technical parts, used as the contract for the mock SOAP provider in the
integration test suite.

## Endpoints

- Test: `https://sbwna92f.sheba.gov.il/csp/healthshare/sms/services/Sheba.CoreInterfaces.BS.SMS.Direct1.cls`
- Production: `https://healthconnect.sheba.gov.il/csp/healthshare/sms/services/Sheba.CoreInterfaces.BS.SMS.Direct1.cls`
- HTTPS only, port 443. Callers need firewall access and the Sheba general certificate installed.
- If the calling system has only a production environment, ALL testing goes against the Ensemble
  TEST environment; production only after tests are approved.
- The WSDL file itself (`LAN TEST-Sheba.CoreInterfaces.BS.SMS.Direct1.WSDL.XML`) was an embedded
  attachment and is not available here; the request/response samples below are the contract.

## Authentication

Mandatory WS-Security UsernameToken in the SOAP header (user name supplied by Sheba; password sent
to the vendor by SMS):

```xml
<soapenv:Header>
  <Security xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
    <UsernameToken>
      <Username>User</Username>
      <Password Type="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText">XXXXXX</Password>
    </UsernameToken>
  </Security>
</soapenv:Header>
```

## Message fields (all in namespace `http://tempuri.org`)

| Field | Required | Notes |
|---|---|---|
| Phone | yes | Mobile number, digits only, no dash (e.g. `0525595069`) |
| Message | yes | Free text, up to 1000 characters (Hebrew allowed) |
| MessageType | yes | Customer-specific type name supplied by Sheba (sample: `SmsType1`) |
| SendingSystem | yes | Free text, name of the sending system (sample: `AppName`) |
| TZ | no* | Recipient national ID |
| FirstName | no* | Recipient first name |
| LastName | no* | Recipient last name |

\* For messages sent to a defined person these are mandatory; otherwise send a fixed placeholder
character (the sample uses `P` for TZ).

## Sample request

HTTP headers:

```
Content-Type: text/xml;charset=UTF-8
SOAPAction: "http://tempuri.org/Sheba.CoreInterfaces.BS.SMS.Direct.SendSMS"
```

Body:

```xml
<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:tem="http://tempuri.org">
  <soapenv:Header>
    <Security xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
      <UsernameToken>
        <Username>User</Username>
        <Password Type="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText">XXXXXX</Password>
      </UsernameToken>
    </Security>
  </soapenv:Header>
  <soapenv:Body>
    <tem:SendSMS>
      <tem:SendingSystem>AppName</tem:SendingSystem>
      <tem:MessageType>SmsType1</tem:MessageType>
      <tem:Message>Test1</tem:Message>
      <tem:FirstName>shay</tem:FirstName>
      <tem:LastName>ma</tem:LastName>
      <tem:TZ>P</tem:TZ>
      <tem:Phone>0525595069</tem:Phone>
    </tem:SendSMS>
  </soapenv:Body>
</soapenv:Envelope>
```

## Sample response (success)

```xml
<SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xmlns:s="http://www.w3.org/2001/XMLSchema">
  <SOAP-ENV:Body>
    <SendSMSResponse xmlns="http://tempuri.org">
      <SendSMSResult>
        <Success>true</Success>
        <ErrorMessage/>
      </SendSMSResult>
    </SendSMSResponse>
  </SOAP-ENV:Body>
</SOAP-ENV:Envelope>
```

Semantic success is `SendSMSResult/Success = true`. A failure is assumed to come back as
`<Success>false</Success>` with text in `<ErrorMessage>` (not shown in the spec; the mock models it
this way), possibly with HTTP 200 - so HTTP status alone is never proof of delivery.
