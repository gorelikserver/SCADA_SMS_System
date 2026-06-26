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
json = json & "  }" & NL
json = json & "}" & NL

Set fso = CreateObject("Scripting.FileSystemObject")
Dim f
Set f = fso.CreateTextFile(installDir & "appsettings.Production.json", True, False)
f.Write json
f.Close
Set fso = Nothing
