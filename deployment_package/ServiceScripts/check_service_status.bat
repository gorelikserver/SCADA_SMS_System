@echo off
REM =====================================================================
REM SCADA SMS System - Service Status Checker
REM =====================================================================
REM Quick status check without requiring Administrator privileges
REM =====================================================================

SET SERVICE_NAME=SCADASMSSystem

cls
echo.
echo =====================================================================
echo  SCADA SMS System - Service Status
echo =====================================================================
echo.

REM Check if service exists
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorLevel% neq 0 (
    echo Service Status: NOT INSTALLED
    echo.
    echo The SCADA SMS Service is not installed on this system.
    echo To install it, run install_service.bat as Administrator.
    echo.
    goto end
)

REM Get detailed service status
sc query "%SERVICE_NAME%" | find "STATE" | find "RUNNING" >nul
if %errorLevel% equ 0 (
    echo Service Status: RUNNING ? 
    echo.
    echo The service is running normally.
) else (
    sc query "%SERVICE_NAME%" | find "STATE" | find "STOPPED" >nul
    if %errorLevel% equ 0 (
        echo Service Status: STOPPED ?
        echo.
        echo The service is stopped.
        echo To start it, run: sc start %SERVICE_NAME% (as Administrator^)
    ) else (
        echo Service Status: UNKNOWN
        echo.
    )
)

echo.
echo Service Information:
echo =====================================================================
sc qc "%SERVICE_NAME%" 2>nul | findstr /C:"DISPLAY_NAME" /C:"BINARY_PATH_NAME" /C:"START_TYPE"

echo.
echo Recent Activity:
echo =====================================================================

REM Check for recent log files
if exist "C:\SCADA\Logs\" (
    echo Log Directory: C:\SCADA\Logs\
    echo.
    
    REM Find the latest log file
    for /f "delims=" %%f in ('dir /b /o-d "C:\SCADA\Logs\scada-sms-*.log" 2^>nul') do (
        set LATEST_LOG=C:\SCADA\Logs\%%f
        goto foundlog
    )
    :foundlog
    
    if defined LATEST_LOG (
        echo Latest Log File: %LATEST_LOG%
        for %%A in ("%LATEST_LOG%") do (
            echo File Size: %%~zA bytes
            echo Last Modified: %%~tA
        )
        echo.
        echo Last 10 log entries:
        echo -------------------------------------------------------------------
        powershell -Command "if (Test-Path '%LATEST_LOG%') { Get-Content '%LATEST_LOG%' -Tail 10 } else { Write-Host 'Log file not accessible' }" 2>nul
    ) else (
        echo No log files found
    )
) else (
    echo Log directory not found: C:\SCADA\Logs\
    echo Service may not have been started yet.
)

echo.
echo =====================================================================

REM Check if web interface is accessible
echo.
echo Checking Web Interface:
echo =====================================================================

REM Try to check port 5000
netstat -ano | findstr :5000 | findstr LISTENING >nul 2>&1
if %errorLevel% equ 0 (
    echo ? HTTP interface appears to be listening on port 5000
    echo   Access at: http://localhost:5000
) else (
    echo ? HTTP interface not detected on port 5000
)

REM Try to check port 5001
netstat -ano | findstr :5001 | findstr LISTENING >nul 2>&1
if %errorLevel% equ 0 (
    echo ? HTTPS interface appears to be listening on port 5001
    echo   Access at: https://localhost:5001
) else (
    echo ? HTTPS interface not detected on port 5001
)

echo.
echo =====================================================================
echo.

REM Check health endpoint
echo Checking Health Endpoint:
echo =====================================================================
powershell -Command "try { $response = Invoke-WebRequest -Uri 'http://localhost:5000/health' -UseBasicParsing -TimeoutSec 5; Write-Host '? Health check: PASSED' -ForegroundColor Green; Write-Host 'Status:' $response.StatusCode } catch { Write-Host '? Health check: FAILED' -ForegroundColor Red; Write-Host 'Error:' $_.Exception.Message }" 2>nul

:end
echo.
echo =====================================================================
echo.
echo For service management, run: manage_service.bat (as Administrator^)
echo.
pause
