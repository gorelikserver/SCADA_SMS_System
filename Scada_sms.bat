@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion
:: SCADA SMS Integration - Hebrew & Unicode Support
:: Parameter order: MESSAGE GROUP_ID [VALUE] [ALARM_ID] [PRIORITY]
:: Example: scada_sms.bat "בדיקה" 3
:: File must be saved as UTF-8 with BOM

if "%~1"=="" exit /b 1
if "%~2"=="" exit /b 1

set "MSG=%~1"
set "GRP=%~2"
set "VALUE=%~3"
set "ALM=%~4"
set "PRI=%~5"
if not defined ALM set "ALM=SCADA-%RANDOM%"
if not defined PRI set "PRI=normal"

:: Validate group ID is numeric
echo %GRP%| findstr /r "^[0-9]*$" >nul || exit /b 1

:: Escape quotes for JSON in Message and Value
set "MSG=!MSG:"=\"!"
if defined VALUE set "VALUE=!VALUE:"=\"!"

:: Smart curl detection: try bundled first, then system
set "CURL_PATH=%~dp0..\Tools\curl.exe"
if not exist "%CURL_PATH%" set "CURL_PATH=curl.exe"

:: Create temporary JSON file to avoid batch escaping issues.
:: Value is a first-class SCADA payload field (e.g. tag reading at the time of alarm).
:: It is NOT auto-appended to Message anymore \u2014 use the {Value} placeholder in your
:: REST/SOAP body template (Settings \u2192 Body Parameters) to embed it.
set "TEMP_JSON=%TEMP%\scada_sms_%RANDOM%.json"
if defined VALUE (
    (
        echo {"Message":"!MSG!","GroupId":%GRP%,"Value":"!VALUE!","AlarmId":"%ALM%","Priority":"%PRI%"
        echo }
    ) > "%TEMP_JSON%"
) else (
    (
        echo {"Message":"!MSG!","GroupId":%GRP%,"AlarmId":"%ALM%","Priority":"%PRI%"
        echo }
    ) > "%TEMP_JSON%"
)

:: Send SMS using JSON file
"%CURL_PATH%" -X POST http://localhost:5000/api/sms/send -H "Content-Type: application/json; charset=utf-8" --data-binary "@%TEMP_JSON%" --silent --connect-timeout 5 --max-time 15 >nul 2>&1

set "RESULT=%ERRORLEVEL%"

:: Clean up temp file
del "%TEMP_JSON%" >nul 2>&1

exit /b %RESULT%
