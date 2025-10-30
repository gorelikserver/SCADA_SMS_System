@echo off
REM =====================================================================
REM SCADA SMS System - Windows Service Uninstaller
REM =====================================================================
REM This script removes the SCADA SMS System Windows Service
REM 
REM Requirements: Administrator privileges
REM Usage: Run as Administrator
REM =====================================================================

echo.
echo =====================================================================
echo  SCADA SMS System - Windows Service Uninstaller
echo =====================================================================
echo.

REM Check for Administrator privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script requires Administrator privileges!
    echo Please right-click and select "Run as Administrator"
    echo.
    pause
    exit /b 1
)

SET SERVICE_NAME=SCADASMSSystem

echo [?] Administrator privileges confirmed
echo.

REM Check if service exists
echo [1/3] Checking if service exists...
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorLevel% neq 0 (
    echo.
    echo Service '%SERVICE_NAME%' is not installed.
    echo Nothing to uninstall.
    echo.
    pause
    exit /b 0
)
echo     ? Service found
echo.

REM Confirm uninstallation
echo WARNING: This will permanently remove the service!
echo.
choice /C YN /M "Are you sure you want to uninstall the service?"
if errorlevel 2 (
    echo.
    echo Uninstallation cancelled.
    pause
    exit /b 0
)
echo.

REM Stop service
echo [2/3] Stopping service...
sc query "%SERVICE_NAME%" | find "STOPPED" >nul
if %errorLevel% equ 0 (
    echo     ? Service already stopped
) else (
    sc stop "%SERVICE_NAME%"
    if %errorLevel% neq 0 (
        echo.
        echo WARNING: Failed to stop service
        echo The service may already be stopped or not responding
        echo.
    ) else (
        echo     ? Service stopped
        echo     Waiting for service to fully stop...
        timeout /t 5 /nobreak >nul
    )
)
echo.

REM Delete service
echo [3/3] Uninstalling service...
sc delete "%SERVICE_NAME%"
if %errorLevel% neq 0 (
    echo.
    echo ERROR: Failed to delete service!
    echo The service may be in use or require a system restart.
    echo.
    pause
    exit /b 1
)
echo     ? Service uninstalled successfully
echo.

echo =====================================================================
echo  SUCCESS: Service has been removed
echo =====================================================================
echo.
echo The service '%SERVICE_NAME%' has been uninstalled.
echo.
echo Note: Application files and logs have NOT been deleted.
echo You can manually delete them if needed:
echo   - Application files: %~dp0
echo   - Log files: C:\SCADA\Logs\
echo.
pause
