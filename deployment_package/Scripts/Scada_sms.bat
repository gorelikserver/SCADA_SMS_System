@echo off
setlocal
:: SCADA SMS Integration Script - Smart curl Detection
:: Tries bundled curl first, falls back to system curl
:: Works on both air-gapped and modern Windows systems
if "%~1"=="" exit /b 1
if "%~2"=="" exit /b 1
set "MSG=%~1"
set "GRP=%~2" 
set "VALUE=%~3"
set "ALM=%~4"
set "PRI=%~5"
if not defined ALM set "ALM=SCADA-%RANDOM%"
if not defined PRI set "PRI=normal"
echo %GRP%| findstr /r "^[0-9]*$" >nul || exit /b 1

:: Append VALUE parameter to message if provided
if defined VALUE (
    set "MSG=%MSG% - %VALUE%"
)
set "MSG=%MSG:"=\"%"

:: Smart curl detection: try bundled first, then system
set "CURL_PATH=%~dp0..\Tools\curl.exe"
if not exist "%CURL_PATH%" set "CURL_PATH=curl.exe"

"%CURL_PATH%" -X POST http://localhost:5000/api/sms/send -H "Content-Type: application/json" -d "{\"message\":\"%MSG%\",\"groupId\":%GRP%,\"alarmId\":\"%ALM%\",\"priority\":\"%PRI%\"}" --silent --connect-timeout 5 --max-time 15 >nul 2>&1
exit /b %errorlevel%
