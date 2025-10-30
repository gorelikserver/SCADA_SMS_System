@echo off
setlocal
:: Optimized SCADA SMS - VALUE as 3rd parameter
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

curl.exe -X POST http://localhost:5000/api/sms/send -H "Content-Type: application/json" -d "{\"message\":\"%MSG%\",\"groupId\":%GRP%,\"alarmId\":\"%ALM%\",\"priority\":\"%PRI%\"}" --silent --connect-timeout 5 --max-time 15 >nul 2>&1
exit /b %errorlevel%
