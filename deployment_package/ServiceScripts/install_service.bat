@echo off
REM ============================================================================
REM  SCADA SMS System - Windows Service Installation
REM  
REM  Purpose: Installs the SCADA SMS System as a Windows Service
REM  Requirements: Administrator privileges
REM  Service Name: SCADASMSSystem
REM  Display Name: SCADA SMS Notification System
REM ============================================================================

setlocal EnableDelayedExpansion
title SCADA SMS System - Service Installation

REM Check for administrator privileges
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ============================================
    echo  ERROR: Administrator privileges required
    echo ============================================
    echo.
    echo Please run this script as Administrator:
    echo 1. Right-click on install_service.bat
    echo 2. Select "Run as administrator"
    echo.
    pause
    exit /b 1
)

echo.
echo ============================================================================
echo  SCADA SMS System - Service Installation
echo ============================================================================
echo.

REM Get the current directory (where the script is located)
set "SCRIPT_DIR=%~dp0"
set "APP_DIR=%SCRIPT_DIR%..\Application"
set "EXE_PATH=%APP_DIR%\SCADASMSSystem.Web.exe"

REM Resolve to absolute path
pushd "%APP_DIR%"
set "FULL_APP_DIR=%CD%"
popd
set "FULL_EXE_PATH=%FULL_APP_DIR%\SCADASMSSystem.Web.exe"

echo [1/8] Validating installation files...
echo.
echo Application Directory: %FULL_APP_DIR%
echo Executable Path: %FULL_EXE_PATH%
echo.

REM Check if executable exists
if not exist "%FULL_EXE_PATH%" (
    echo [ERROR] Application executable not found!
    echo Expected: %FULL_EXE_PATH%
    echo.
    echo Please ensure the application is built and deployed correctly.
    pause
    exit /b 1
)

REM Check if appsettings.json exists
if not exist "%FULL_APP_DIR%\appsettings.json" (
    echo [WARNING] appsettings.json not found!
    echo The application may fail to start without proper configuration.
    echo.
)

echo [OK] Installation files validated
echo.

REM Check if service already exists
echo [2/8] Checking for existing service...
sc query SCADASMSSystem >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [WARNING] Service already exists!
    echo.
    choice /C YN /M "Do you want to remove and reinstall the service"
    if errorlevel 2 (
        echo Installation cancelled.
        pause
        exit /b 0
    )
    
    echo Stopping existing service...
    sc stop SCADASMSSystem >nul 2>&1
    timeout /t 3 >nul
    
    echo Removing existing service...
    sc delete SCADASMSSystem
    timeout /t 2 >nul
    echo [OK] Existing service removed
) else (
    echo [OK] No existing service found
)
echo.

REM Create the service
echo [3/8] Creating Windows Service...
sc create SCADASMSSystem binPath= "%FULL_EXE_PATH%" DisplayName= "SCADA SMS Notification System" start= auto

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to create service!
    echo.
    echo Common causes:
    echo  - Service name already in use
    echo  - Invalid executable path
    echo  - Insufficient permissions
    echo.
    pause
    exit /b 1
)
echo [OK] Service created successfully
echo.

REM Set service description
echo [4/8] Setting service description...
sc description SCADASMSSystem "Industrial SCADA SMS notification system for alarm management and operator notifications"
echo [OK] Description set
echo.

REM Configure service recovery options (auto-restart on failure)
echo [5/8] Configuring auto-restart on failure...
sc failure SCADASMSSystem reset= 86400 actions= restart/60000/restart/60000/restart/60000
echo [OK] Auto-restart configured
echo    - First failure: Restart after 1 minute
echo    - Second failure: Restart after 1 minute  
echo    - Third failure: Restart after 1 minute
echo    - Reset counter: After 24 hours
echo.

REM Ensure log directory exists
echo [6/8] Creating log directory...
set "LOG_DIR=C:\SCADA\Logs"
if not exist "%LOG_DIR%" (
    mkdir "%LOG_DIR%" 2>nul
    if exist "%LOG_DIR%" (
        echo [OK] Log directory created: %LOG_DIR%
    ) else (
        echo [WARNING] Could not create log directory: %LOG_DIR%
        echo Service may fail if logging is required
    )
) else (
    echo [OK] Log directory already exists: %LOG_DIR%
)
echo.

REM Display service configuration
echo [7/8] Service Configuration:
echo.
sc qc SCADASMSSystem
echo.

REM Start the service
echo [8/8] Starting service...
sc start SCADASMSSystem

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Service failed to start!
    echo.
    echo Troubleshooting steps:
    echo 1. Check Event Viewer (eventvwr.msc) for error details
    echo    - Windows Logs ^> Application
    echo    - Look for errors from ".NET Runtime" or "SCADA SMS System"
    echo.
    echo 2. Test the executable directly:
    echo    cd "%FULL_APP_DIR%"
    echo    SCADASMSSystem.Web.exe
    echo.
    echo 3. Common issues:
    echo    - Database connection string incorrect (check appsettings.json)
    echo    - Port 5000/5001 already in use
    echo    - Missing configuration files
    echo    - Insufficient file permissions
    echo.
    echo 4. View service status:
    echo    sc query SCADASMSSystem
    echo.
    pause
    exit /b 1
)

REM Wait a moment and check if service is running
timeout /t 3 >nul
sc query SCADASMSSystem | find "RUNNING" >nul
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================================================
    echo  Installation Successful!
    echo ============================================================================
    echo.
    echo Service Name: SCADASMSSystem
    echo Display Name: SCADA SMS Notification System
    echo Status: RUNNING
    echo.
    echo Web Interface: http://localhost:5000
    echo Health Check: http://localhost:5000/health
    echo.
    echo Log Files: %LOG_DIR%
    echo.
    echo Next Steps:
    echo  1. Open browser: http://localhost:5000
    echo  2. Verify application is working
    echo  3. Configure SMS settings in web interface
    echo  4. Test SMS functionality
    echo.
    echo Service Management:
    echo  - Start: sc start SCADASMSSystem
    echo  - Stop: sc stop SCADASMSSystem
    echo  - Status: sc query SCADASMSSystem
    echo  - Manage: manage_service.bat
    echo.
    echo ============================================================================
) else (
    echo.
    echo [WARNING] Service created but not running!
    echo.
    echo Current Status:
    sc query SCADASMSSystem
    echo.
    echo Please check Event Viewer for error details:
    echo  eventvwr.msc ^> Windows Logs ^> Application
    echo.
    echo Or run diagnostics:
    echo  diagnose_service.bat
    echo.
)

pause
