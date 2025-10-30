@echo off
title SCADA SMS System - Log Viewer
echo =====================================
echo SCADA SMS System - Log Diagnostics
echo =====================================
echo.

echo Checking Windows Event Logs for SCADA SMS System...
echo.

REM Check Application Event Log
echo === APPLICATION EVENT LOG ===
powershell -Command "Get-WinEvent -LogName 'Application' -MaxEvents 20 | Where-Object {$_.ProviderName -like '*SCADA*' -or $_.Message -like '*SMS*' -or $_.Message -like '*SmsService*'} | Format-Table TimeCreated, LevelDisplayName, Message -AutoSize"

echo.
echo === SYSTEM EVENT LOG ===
powershell -Command "Get-WinEvent -LogName 'System' -MaxEvents 20 | Where-Object {$_.Message -like '*SCADA*'} | Format-Table TimeCreated, LevelDisplayName, Message -AutoSize"

echo.
echo === Checking for log files in application directory ===
if exist "logs\*.log" (
    echo Found log files:
    dir logs\*.log /b
    echo.
    echo Latest log entries:
    powershell -Command "Get-Content 'logs\*.log' -Tail 50 | Select-Object -Last 20"
) else (
    echo No log files found in logs\ directory
)

echo.
echo Press any key to continue...
pause > nul