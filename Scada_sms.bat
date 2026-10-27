@echo off
chcp 65001 >nul 2>&1
setlocal DisableDelayedExpansion
:: SCADA SMS Integration - Hebrew & Unicode Support
:: Parameter order: MESSAGE GROUP_ID [VALUE] ALARM_ID [PRIORITY]
:: Example: scada_sms.bat "בדיקה" 3 "" "ALM-12345"
:: File must be saved as UTF-8 with BOM

if "%~1"=="" exit /b 1
if "%~2"=="" exit /b 1
if "%~4"=="" exit /b 1

set "MSG=%~1"
set "GRP=%~2"
set "VALUE=%~3"
set "ALM=%~4"
set "PRI=%~5"
if not defined PRI set "PRI=normal"

:: Generate collision-resistant IDs with built-in Windows PowerShell.
for /f "usebackq delims=" %%I in (`powershell.exe -NoProfile -NonInteractive -Command "[Guid]::NewGuid().ToString('N')"`) do set "CALL_ID=%%I"
if not defined CALL_ID exit /b 1

:: Validate group ID is numeric
echo %GRP%| findstr /r "^[0-9]*$" >nul || exit /b 1

:: Smart curl detection: try bundled first, then system
set "CURL_PATH=%~dp0..\Tools\curl.exe"
if not exist "%CURL_PATH%" set "CURL_PATH=curl.exe"

:: Build JSON through PowerShell serializer. Environment variables preserve Unicode and
:: avoid hand-written JSON escaping for quotes, backslashes, and control characters.
:: Value is a first-class SCADA payload field (e.g. tag reading at the time of alarm).
:: It is NOT auto-appended to Message anymore \u2014 use the {Value} placeholder in your
:: REST/SOAP body template (Settings \u2192 Body Parameters) to embed it.
set "TEMP_JSON=%TEMP%\scada_sms_%CALL_ID%.json"
set "TEMP_STATUS=%TEMP%\scada_sms_%CALL_ID%.status"
set "SCADA_MSG=%MSG%"
set "SCADA_GRP=%GRP%"
set "SCADA_VALUE=%VALUE%"
set "SCADA_ALM=%ALM%"
set "SCADA_PRI=%PRI%"
set "SCADA_JSON=%TEMP_JSON%"
powershell.exe -NoProfile -NonInteractive -Command "$p = [ordered]@{ Message=$env:SCADA_MSG; GroupId=[int]$env:SCADA_GRP; AlarmId=$env:SCADA_ALM; Priority=$env:SCADA_PRI }; if ($env:SCADA_VALUE) { $p.Value=$env:SCADA_VALUE }; $json=$p | ConvertTo-Json -Compress; $utf8=New-Object System.Text.UTF8Encoding($false); [IO.File]::WriteAllText($env:SCADA_JSON,$json,$utf8)" >nul 2>&1
if errorlevel 1 exit /b 1

:: Wait without a curl total timeout. Server/provider calls have their own bounded timeout,
:: and SCADA must not receive success before every recipient is accepted.
"%CURL_PATH%" -X POST http://localhost:5000/api/sms/send -H "Content-Type: application/json; charset=utf-8" --data-binary "@%TEMP_JSON%" --silent --show-error --output nul --write-out "%%{http_code}" --connect-timeout 5 --max-time 0 > "%TEMP_STATUS%" 2>nul

set "RESULT=%ERRORLEVEL%"
set "HTTP_STATUS="
if exist "%TEMP_STATUS%" set /p HTTP_STATUS=<"%TEMP_STATUS%"

:: Clean up temp file
del "%TEMP_JSON%" >nul 2>&1
del "%TEMP_STATUS%" >nul 2>&1

if not "%RESULT%"=="0" exit /b %RESULT%
if not defined HTTP_STATUS exit /b 1
set /a HTTP_STATUS_NUM=%HTTP_STATUS% >nul 2>&1
if %HTTP_STATUS_NUM% LSS 200 exit /b 22
if %HTTP_STATUS_NUM% GEQ 300 exit /b 22

exit /b 0
