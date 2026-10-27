On Error Resume Next

Dim data, pairs, pair, kv
Dim installDir, dbServer, dbName, dbAuth, dbUser, dbPassword, port, logDir, pcimDir, configDir

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
pcimDir    = Trim(props("PCIMUTIL_DIR"))
If Len(pcimDir) > 0 And Right(pcimDir, 1) <> "\" Then pcimDir = pcimDir & "\"

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

' Log folder of the service. Empty keeps the app default (C:\SCADA\Logs).
logDir = Trim(logDir)
If Len(logDir) > 0 And Right(logDir, 1) <> "\" Then logDir = logDir & "\"

' The DB password is kept in CONFIGDIR (SYSTEM and Administrators only), not in the registry.
' On upgrade the dialog starts empty; empty means "keep the saved password".
Set fso = CreateObject("Scripting.FileSystemObject")
' The CONFIGDIR the MSI creates with that ACL, also where the app keeps settings.json.
configDir = CreateObject("WScript.Shell").ExpandEnvironmentStrings("%ProgramData%") & "\SCADA_SMS\config\"
Dim pwFile : pwFile = configDir & "installer-db-password.txt"
Dim pwStream
If Len(dbPassword) = 0 And fso.FileExists(pwFile) Then
    Set pwStream = fso.OpenTextFile(pwFile, 1, False, -1)
    If Not pwStream.AtEndOfStream Then dbPassword = pwStream.ReadAll
    pwStream.Close
End If
If Len(configDir) > 0 Then
    CreateFolders configDir
    Set pwStream = fso.CreateTextFile(pwFile, True, True)
    pwStream.Write dbPassword
    pwStream.Close
End If

Dim connStr
If UCase(Trim(dbAuth)) = "WINDOWS" Then
    connStr = "Server=" & dbServer & ";Database=" & dbName & _
              ";Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5;"
Else
    connStr = "Server=" & dbServer & ";Database=" & dbName & _
              ";User Id=" & dbUser & ";Password=" & dbPassword & _
              ";TrustServerCertificate=true;Connect Timeout=5;"
End If

Dim connStrJson : connStrJson = JsonEscape(connStr)

Dim Q : Q = Chr(34)
Dim NL : NL = Chr(13) & Chr(10)

Dim json
json = "{" & NL
json = json & "  " & Q & "ConnectionStrings" & Q & ": {" & NL
json = json & "    " & Q & "DefaultConnection" & Q & ": " & Q & connStrJson & Q & NL
json = json & "  }," & NL
json = json & "  " & Q & "Urls" & Q & ": " & Q & "http://localhost:" & port & Q & "," & NL
If Len(logDir) > 0 Then
    json = json & "  " & Q & "Logging" & Q & ": { " & Q & "File" & Q & ": { " & Q & "Path" & Q & ": " & _
           Q & JsonEscape(logDir & "scada-sms-.log") & Q & " } }," & NL
End If
' Tells the app where Akara runs Scada_sms.bat from, so it can warn when that copy is stale.
json = json & "  " & Q & "Scada" & Q & ": {" & NL
json = json & "    " & Q & "PcimUtilDir" & Q & ": " & Q & JsonEscape(pcimDir) & Q & NL
json = json & "  }" & NL
json = json & "}" & NL

Dim f
If Len(logDir) > 0 Then CreateFolders logDir
Set f = fso.CreateTextFile(installDir & "appsettings.Production.json", True, False)
f.Write json
f.Close

Dim batUrl : batUrl = "http://localhost:" & port & "/api/sms/send"

' Scada_sms.ini next to the installed Scada_sms.bat: point the bat at the same port.
WriteIni installDir & "Scripts\Scada_sms.ini", batUrl, ""

' Akara runs getalias('PCIMUTIL') + 'Scada_sms.bat', so the bat must also sit in that folder.
' Copied here, not installed as an MSI file: uninstall and the remove step of a major upgrade
' must not take it away, or alarms raised meanwhile could not even be spooled. The copy's ini
' points curl at the bundled one, since ..\Tools does not exist next to Akara's folder.
' Failures are not fatal; the app shows a banner when this copy is missing or outdated.
If Len(pcimDir) > 0 Then
    If Not fso.FolderExists(pcimDir) Then fso.CreateFolder pcimDir
    fso.CopyFile installDir & "Scripts\Scada_sms.bat", pcimDir & "Scada_sms.bat", True
    Dim curlPath : curlPath = ""
    If fso.FileExists(installDir & "Tools\curl.exe") Then curlPath = installDir & "Tools\curl.exe"
    WriteIni pcimDir & "Scada_sms.ini", batUrl, curlPath
End If
Set fso = Nothing

' The file is written as ASCII (FSO cannot write UTF-8), so anything else is escaped as
' \uXXXX; a non-ASCII character would otherwise make the write fail and leave the file empty.
Function JsonEscape(text)
    Dim i, ch, code, out
    out = ""
    For i = 1 To Len(text)
        ch = Mid(text, i, 1)
        code = AscW(ch) And &HFFFF&
        If ch = "\" Then
            out = out & "\\"
        ElseIf ch = Chr(34) Then
            out = out & "\" & Chr(34)
        ElseIf code < 32 Or code > 126 Then
            out = out & "\u" & Right("000" & Hex(code), 4)
        Else
            out = out & ch
        End If
    Next
    JsonEscape = out
End Function

' CreateFolder needs the parent to exist; LOG_DIR may be several levels deep.
Sub CreateFolders(path)
    Dim p : p = path
    If Right(p, 1) = "\" Then p = Left(p, Len(p) - 1)
    If Len(p) = 0 Or fso.FolderExists(p) Then Exit Sub
    CreateFolders fso.GetParentFolderName(p)
    fso.CreateFolder p
End Sub

' Writes URL= (and CURL= when given) and keeps every other line an engineer added
' (SPOOL_ROOT=, MAXTIME=, comments).
Sub WriteIni(iniPath, url, curl)
    Dim iniLines, iniLine, iniOut, ts, key
    iniOut = "URL=" & url & NL
    If Len(curl) > 0 Then iniOut = iniOut & "CURL=" & curl & NL
    If fso.FileExists(iniPath) Then
        Set ts = fso.OpenTextFile(iniPath, 1, False)
        iniLines = Split(Replace(ts.ReadAll, Chr(13), ""), Chr(10))
        ts.Close
        For Each iniLine In iniLines
            key = UCase(Left(LTrim(iniLine), 5))
            If Len(Trim(iniLine)) > 0 And Left(key, 4) <> "URL=" And Not (Len(curl) > 0 And key = "CURL=") Then
                iniOut = iniOut & iniLine & NL
            End If
        Next
    Else
        iniOut = "; Written by the Scada SMS installer. KEY=VALUE, keys: URL, SPOOL_ROOT, MAXTIME, CURL." & NL & iniOut
    End If
    Set ts = fso.CreateTextFile(iniPath, True, False)
    ts.Write iniOut
    ts.Close
End Sub
