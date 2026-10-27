@echo off
chcp 65001 >nul 2>&1
setlocal DisableDelayedExpansion
:: SCADA SMS Integration - Hebrew & Unicode Support
:: Akara calls:  Scada_sms.bat MESSAGE GROUP_ID VALUE
:: Optional (manual/testing only): 4th = ALARM_ID, 5th = PRIORITY
:: Example: scada_sms.bat "Pump failure" 3 "85.3"   (Hebrew text works: values never pass through this file)
:: Keep this file pure ASCII without a BOM, CRLF line endings: cmd.exe would run a BOM as
:: part of the first command, so "@echo off" would fail.
::
:: Delivery contract:
::   1. The alarm is written to the spool folder BEFORE anything else.
::   2. It is POSTed to the local service, which sends it to every recipient.
::   3. The spool file is deleted only when the service confirms every recipient
::      was accepted by the provider (HTTP 200). Otherwise the service picks the
::      file up later and retries the recipients that failed (same AlarmId).
:: Akara ignores the exit code; Scada_sms.log next to the spool is the record.
::
:: Exit codes: 0 all accepted | 1 bad arguments or spool write failed
::             2 service unreachable or timed out | 3 delivery failed or partial
::             4 rejected by service

if "%~1"=="" exit /b 1
if "%~2"=="" exit /b 1

set "MSG=%~1"
set "GRP=%~2"
set "VALUE=%~3"
set "ALM=%~4"
set "PRI=%~5"
if not defined PRI set "PRI=normal"

:: Validate group ID is numeric
echo %GRP%| findstr /r "^[0-9][0-9]*$" >nul || exit /b 1

:: Defaults, overridable by Scada_sms.ini next to this file (KEY=VALUE lines)
set "URL=http://localhost:5000/api/sms/send"
set "SPOOL_ROOT=%ProgramData%\SCADA_SMS"
set "MAXTIME=120"
if exist "%~dp0Scada_sms.ini" (
    for /f "usebackq eol=; tokens=1,* delims==" %%A in ("%~dp0Scada_sms.ini") do (
        if /i "%%A"=="URL" set "URL=%%B"
        if /i "%%A"=="SPOOL_ROOT" set "SPOOL_ROOT=%%B"
        if /i "%%A"=="MAXTIME" set "MAXTIME=%%B"
    )
)
set "SPOOL_DIR=%SPOOL_ROOT%\spool"
set "LOG_FILE=%SPOOL_ROOT%\Scada_sms.log"
if not exist "%SPOOL_DIR%" mkdir "%SPOOL_DIR%" >nul 2>&1

:: Smart curl detection: try bundled first, then system
set "CURL_PATH=%~dp0..\Tools\curl.exe"
if not exist "%CURL_PATH%" set "CURL_PATH=curl.exe"

:: One PowerShell call: pick the AlarmId (generated when SCADA gives none), serialize
:: the JSON safely (Unicode, quotes, backslashes), write it to the spool atomically
:: (.tmp then rename) and print the AlarmId. Values travel through environment
:: variables, never through the command line.
set "SCADA_MSG=%MSG%"
set "SCADA_GRP=%GRP%"
set "SCADA_VALUE=%VALUE%"
set "SCADA_ALM=%ALM%"
set "SCADA_PRI=%PRI%"
set "SCADA_SPOOL=%SPOOL_DIR%"
set "ALM="
for /f "usebackq delims=" %%I in (`powershell.exe -NoProfile -NonInteractive -Command "$id = if ($env:SCADA_ALM) { $env:SCADA_ALM } else { 'SCADA-' + [Guid]::NewGuid().ToString('N') }; $p = [ordered]@{ Message=$env:SCADA_MSG; GroupId=[int]$env:SCADA_GRP; AlarmId=$id; Priority=$env:SCADA_PRI; ReceivedAt=[DateTime]::UtcNow.ToString('o') }; if ($env:SCADA_VALUE) { $p.Value=$env:SCADA_VALUE }; $safe = ($id -replace '[^A-Za-z0-9_.-]', '_'); $tmp = Join-Path $env:SCADA_SPOOL ($safe + '.tmp'); $dst = Join-Path $env:SCADA_SPOOL ($safe + '.json'); $utf8 = New-Object System.Text.UTF8Encoding($false); [IO.File]::WriteAllText($tmp, (ConvertTo-Json -InputObject $p -Compress), $utf8); Move-Item -LiteralPath $tmp -Destination $dst -Force; $safe"`) do set "ALM=%%I"
if not defined ALM (
    >>"%LOG_FILE%" echo %DATE% %TIME% group=%GRP% exit=1 spool-write-failed
    exit /b 1
)
set "SPOOL_FILE=%SPOOL_DIR%\%ALM%.json"
if not exist "%SPOOL_FILE%" exit /b 1

:: Wait for the service: it answers only after every provider call finished.
set "TEMP_STATUS=%TEMP%\scada_sms_%ALM%.status"
"%CURL_PATH%" -X POST "%URL%" -H "Content-Type: application/json; charset=utf-8" --data-binary "@%SPOOL_FILE%" --silent --output nul --write-out "%%{http_code}" --connect-timeout 5 --max-time %MAXTIME% > "%TEMP_STATUS%" 2>nul
set "CURL_RC=%ERRORLEVEL%"
set "HTTP_STATUS=000"
if exist "%TEMP_STATUS%" set /p HTTP_STATUS=<"%TEMP_STATUS%"
del "%TEMP_STATUS%" >nul 2>&1

set "RESULT=4"
if not "%CURL_RC%"=="0" set "RESULT=2"
if "%CURL_RC%"=="0" if "%HTTP_STATUS%"=="200" set "RESULT=0"
if "%CURL_RC%"=="0" if "%HTTP_STATUS%"=="502" set "RESULT=3"

:: Only full acceptance removes the alarm from the spool.
if "%RESULT%"=="0" del "%SPOOL_FILE%" >nul 2>&1

>>"%LOG_FILE%" echo %DATE% %TIME% alarm=%ALM% group=%GRP% http=%HTTP_STATUS% curl=%CURL_RC% exit=%RESULT%
exit /b %RESULT%
