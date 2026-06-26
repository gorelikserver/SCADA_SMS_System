@echo off
REM =====================================================================
REM SCADA SMS System - Windows Service Uninstaller (full removal)
REM =====================================================================
REM Removes the Windows Service, the application install folder, and
REM optionally the logs folder (default for logs: KEEP).
REM
REM Requirements: Administrator privileges
REM Usage: Run as Administrator
REM =====================================================================
setlocal EnableExtensions EnableDelayedExpansion

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

set "SERVICE_NAME=SCADASMSSystem"
set "INSTALL_DIR=%~dp0"
if "%INSTALL_DIR:~-1%"=="\" set "INSTALL_DIR=%INSTALL_DIR:~0,-1%"
set "LOGS_DIR=C:\SCADA\Logs"

echo [OK] Administrator privileges confirmed
echo     Install folder : %INSTALL_DIR%
echo     Logs folder    : %LOGS_DIR%
echo.

REM ---- 1. Service existence check ------------------------------------
echo [1/5] Checking if service exists...
sc query "%SERVICE_NAME%" >nul 2>&1
set "SERVICE_EXISTS=1"
if %errorLevel% neq 0 set "SERVICE_EXISTS=0"
if "%SERVICE_EXISTS%"=="0" (
    echo     Service '%SERVICE_NAME%' is not installed.
) else (
    echo     [OK] Service found
)
echo.

REM ---- 2. Confirm overall uninstall ----------------------------------
echo WARNING: This will permanently remove the SCADA SMS System.
echo.
choice /C YN /N /M "Continue with uninstall? (Y/N): "
if errorlevel 2 (
    echo.
    echo Uninstall cancelled by user.
    pause
    exit /b 0
)
echo.

REM ---- 3. Stop and delete service ------------------------------------
if "%SERVICE_EXISTS%"=="1" (
    echo [2/5] Stopping service...
    sc query "%SERVICE_NAME%" | find "STOPPED" >nul
    if !errorLevel! equ 0 (
        echo     [OK] Service already stopped
    ) else (
        sc stop "%SERVICE_NAME%" >nul 2>&1
        echo     Waiting for service to stop...
        timeout /t 5 /nobreak >nul
        echo     [OK] Stop signal sent
    )
    echo.

    echo [3/5] Removing service registration...
    sc delete "%SERVICE_NAME%" >nul 2>&1
    if !errorLevel! neq 0 (
        echo     ERROR: Failed to delete service. Reboot may be required.
        pause
        exit /b 1
    )
    echo     [OK] Service removed
    echo.
) else (
    echo [2/5] Skipped - no service to stop.
    echo [3/5] Skipped - no service to remove.
    echo.
)

REM ---- 4. Confirm and remove install dir -----------------------------
echo [4/5] Application folder removal
echo     Target: %INSTALL_DIR%
echo.
choice /C YN /N /M "Delete the application folder and ALL its contents? (Y/N): "
if errorlevel 2 (
    echo     [SKIP] Application folder kept at: %INSTALL_DIR%
    set "REMOVE_INSTALL=0"
) else (
    set "REMOVE_INSTALL=1"
)
echo.

REM ---- 5. Logs folder prompt (default KEEP) --------------------------
echo [5/5] Log folder removal
echo     Target: %LOGS_DIR%
echo.
if exist "%LOGS_DIR%" (
    echo     NOTE: For audit retention and regulatory compliance,
    echo           keeping the logs folder is recommended.
    choice /C YN /N /M "Delete the LOGS folder? (Y/N): "
    if errorlevel 2 (
        echo     [KEEP] Logs preserved at: %LOGS_DIR%
        set "REMOVE_LOGS=0"
    ) else (
        set "REMOVE_LOGS=1"
    )
) else (
    echo     [INFO] No logs folder found - nothing to delete.
    set "REMOVE_LOGS=0"
)
echo.

REM ---- Execute deletions (logs first, then self-deleting install) ----
if "%REMOVE_LOGS%"=="1" (
    echo Removing logs folder...
    rmdir /S /Q "%LOGS_DIR%" 2>nul
    if exist "%LOGS_DIR%" (
        echo     WARNING: Some files in %LOGS_DIR% could not be removed (in-use?).
    ) else (
        echo     [OK] Logs folder removed.
    )
    echo.
)

echo =====================================================================
echo  Uninstall summary
echo =====================================================================
echo  Service        : REMOVED
if "%REMOVE_INSTALL%"=="1" (
    echo  Install folder : SCHEDULED FOR DELETION  ^(%INSTALL_DIR%^)
) else (
    echo  Install folder : KEPT                    ^(%INSTALL_DIR%^)
)
if "%REMOVE_LOGS%"=="1" (
    echo  Logs folder    : REMOVED                 ^(%LOGS_DIR%^)
) else (
    echo  Logs folder    : KEPT                    ^(%LOGS_DIR%^)
)
echo =====================================================================
echo.

if "%REMOVE_INSTALL%"=="1" (
    echo The application folder will be deleted after this script exits.
    echo Press any key to finish...
    pause >nul
    REM Self-deletion trick: spawn a detached cmd that waits, then deletes the
    REM whole install folder (including this .bat). Cannot delete a folder
    REM containing a running script directly.
    start "" /B cmd /c "timeout /t 2 /nobreak >nul & rmdir /S /Q ""%INSTALL_DIR%"""
    exit /b 0
)

echo Press any key to exit...
pause >nul
exit /b 0
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
