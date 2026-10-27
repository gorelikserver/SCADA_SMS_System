On Error Resume Next

Dim data, pairs, pair, kv
Dim installDir, dbServer, dbName, dbAuth, dbUser, dbPassword, port, logDir

Set props = CreateObject("Scripting.Dictionary")
data = Session.Property("CustomActionData")
pairs = Split(data, ";")
For Each pair In pairs
    kv = Split(pair, "=", 2)
    If UBound(kv) = 1 Then props(kv(0)) = kv(1)
Next

installDir = props("INSTALLDIR")
dbServer   = props("DB_SERVER")
dbName     = props("DB_NAME")
dbAuth     = props("DB_AUTH")
dbUser     = props("DB_USER")
dbPassword = props("DB_PASSWORD")
port       = props("APP_PORT")
logDir     = props("LOG_DIR")

' One validated port for both the service (Urls) and Scada_sms.bat (URL=).
' Without Urls Kestrel listens on http://localhost:5000, so 5000 keeps the old behavior.
port = Trim(port)
Dim portOk : portOk = (Len(port) > 0 And Len(port) <= 5)
If portOk Then
    Dim ci
    For ci = 1 To Len(port)
        If InStr("0123456789", Mid(port, ci, 1)) = 0 Then portOk = False
    Next
End If
If portOk Then portOk = (CLng(port) >= 1 And CLng(port) <= 65535)
If Not portOk Then port = "5000"
port = CStr(CLng(port))

Dim connStr
If UCase(Trim(dbAuth)) = "WINDOWS" Then
    connStr = "Server=" & dbServer & ";Database=" & dbName & _
              ";Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5;"
Else
    connStr = "Server=" & dbServer & ";Database=" & dbName & _
              ";User Id=" & dbUser & ";Password=" & dbPassword & _
              ";TrustServerCertificate=true;Connect Timeout=5;"
End If

Dim connStrJson : connStrJson = Replace(connStr, "\", "\\")

Dim Q : Q = Chr(34)
Dim NL : NL = Chr(13) & Chr(10)

Dim json
json = "{" & NL
json = json & "  " & Q & "ConnectionStrings" & Q & ": {" & NL
json = json & "    " & Q & "DefaultConnection" & Q & ": " & Q & connStrJson & Q & NL
json = json & "  }," & NL
json = json & "  " & Q & "Urls" & Q & ": " & Q & "http://localhost:" & port & Q & NL
json = json & "}" & NL

Set fso = CreateObject("Scripting.FileSystemObject")
Dim f
Set f = fso.CreateTextFile(installDir & "appsettings.Production.json", True, False)
f.Write json
f.Close

' Scada_sms.ini next to the installed Scada_sms.bat: point the bat at the same port.
' Other keys an engineer added (SPOOL_ROOT=, MAXTIME=, comments) are kept.
Dim iniPath, iniLines, iniLine, iniOut, ts
iniPath = installDir & "Scripts\Scada_sms.ini"
iniOut = "URL=http://localhost:" & port & "/api/sms/send" & NL
If fso.FileExists(iniPath) Then
    Set ts = fso.OpenTextFile(iniPath, 1, False)
    iniLines = Split(Replace(ts.ReadAll, Chr(13), ""), Chr(10))
    ts.Close
    For Each iniLine In iniLines
        If Len(Trim(iniLine)) > 0 And UCase(Left(LTrim(iniLine), 4)) <> "URL=" Then
            iniOut = iniOut & iniLine & NL
        End If
    Next
Else
    iniOut = "; Written by the Scada SMS installer. KEY=VALUE, keys: URL, SPOOL_ROOT, MAXTIME." & NL & iniOut
End If
Set ts = fso.CreateTextFile(iniPath, True, False)
ts.Write iniOut
ts.Close
Set fso = Nothing
