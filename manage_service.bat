@echo off
REM =====================================================================
REM SCADA SMS System - Service Management Script
REM =====================================================================
REM Provides quick access to common service management tasks
REM 
REM Requirements: Administrator privileges
REM Usage: Run as Administrator
REM =====================================================================

SET SERVICE_NAME=SCADASMSSystem

:menu
cls
echo.
echo =====================================================================
echo  SCADA SMS System - Service Management
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

REM Get service status
sc query "%SERVICE_NAME%" >nul 2>&1
if %errorLevel% neq 0 (
    echo Service Status: NOT INSTALLED
    echo.
    echo Available options:
    echo   [I] Install Service
    echo   [Q] Quit
    echo.
    choice /C IQ /N /M "Select option: "
    if errorlevel 2 exit /b 0
    if errorlevel 1 goto install
) else (
    sc query "%SERVICE_NAME%" | find "RUNNING" >nul
    if %errorLevel% equ 0 (
        echo Service Status: RUNNING [?]
    ) else (
        sc query "%SERVICE_NAME%" | find "STOPPED" >nul
        if %errorLevel% equ 0 (
            echo Service Status: STOPPED [?]
        ) else (
            echo Service Status: UNKNOWN
        )
    )
    echo.
    echo Available options:
    echo   [1] Start Service
    echo   [2] Stop Service
    echo   [3] Restart Service
    echo   [4] View Service Status
    echo   [5] View Recent Logs
    echo   [6] Open Log Directory
    echo   [7] Uninstall Service
    echo   [Q] Quit
    echo.
    choice /C 1234567Q /N /M "Select option: "
    if errorlevel 8 exit /b 0
    if errorlevel 7 goto uninstall
    if errorlevel 6 goto openlogs
    if errorlevel 5 goto viewlogs
    if errorlevel 4 goto status
    if errorlevel 3 goto restart
    if errorlevel 2 goto stop
    if errorlevel 1 goto start
)

:install
echo.
echo Installing service...
call install_service.bat
goto menu

:start
echo.
echo Starting service...
sc start "%SERVICE_NAME%"
if %errorLevel% equ 0 (
    echo ? Service started successfully
) else (
    echo ? Failed to start service
    echo Check Event Viewer for details
)
timeout /t 3 /nobreak >nul
goto menu

:stop
echo.
echo Stopping service...
sc stop "%SERVICE_NAME%"
if %errorLevel% equ 0 (
    echo ? Service stopped successfully
) else (
    echo ? Failed to stop service
)
timeout /t 3 /nobreak >nul
goto menu

:restart
echo.
echo Restarting service...
sc stop "%SERVICE_NAME%"
timeout /t 3 /nobreak >nul
sc start "%SERVICE_NAME%"
if %errorLevel% equ 0 (
    echo ? Service restarted successfully
) else (
    echo ? Failed to restart service
)
timeout /t 3 /nobreak >nul
goto menu

:status
echo.
echo =====================================================================
echo Service Status Details:
echo =====================================================================
sc query "%SERVICE_NAME%"
echo.
echo =====================================================================
sc qc "%SERVICE_NAME%"
echo.
pause
goto menu

:viewlogs
echo.
echo =====================================================================
echo Recent Log Entries (last 50 lines):
echo =====================================================================
echo.
if exist "C:\SCADA\Logs\" (
    for /f "delims=" %%f in ('dir /b /o-d "C:\SCADA\Logs\scada-sms-*.log" 2^>nul') do (
        set LATEST_LOG=C:\SCADA\Logs\%%f
        goto showlog
    )
    :showlog
    if defined LATEST_LOG (
        echo Log file: %LATEST_LOG%
        echo.
        powershell -Command "Get-Content '%LATEST_LOG%' -Tail 50"
    ) else (
        echo No log files found in C:\SCADA\Logs\
    )
) else (
    echo Log directory not found: C:\SCADA\Logs\
)
echo.
pause
goto menu

:openlogs
echo.
echo Opening log directory...
if exist "C:\SCADA\Logs\" (
    explorer "C:\SCADA\Logs\"
) else (
    echo Log directory not found: C:\SCADA\Logs\
    timeout /t 2 /nobreak >nul
)
goto menu

:uninstall
echo.
echo Uninstalling service...
call uninstall_service.bat
goto menu
