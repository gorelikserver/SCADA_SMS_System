@echo off
REM ============================================================================
REM  SCADA SMS System - Complete Deployment Package Creator
REM  
REM  Purpose: Creates production-ready deployment package with:
REM           - Application binaries (self-contained)
REM           - Windows Service wrapper scripts
REM           - SCADA integration script
REM           - Service management utilities
REM           - curl.exe for air-gapped systems
REM           - Complete documentation
REM  
REM  Version: 2.1
REM  Target: .NET 9 - Windows x64 Self-Contained with Service Support
REM ============================================================================

setlocal EnableDelayedExpansion
title SCADA SMS System - Complete Build & Deployment

REM Set console colors
color 0B

echo.
echo ============================================================================
echo  SCADA SMS System - Complete Deployment Package Creator
echo ============================================================================
echo  Build Date: %DATE% %TIME%
echo  Target: Windows x64 Self-Contained + Windows Service + curl.exe
echo ============================================================================
echo.

REM ------------------------------------------
REM Step 1: Environment Cleanup
REM ------------------------------------------
echo [1/8] Cleaning build environment...

REM Stop any running instances
taskkill /F /IM SCADASMSSystem.Web.exe 2>nul
taskkill /F /IM dotnet.exe 2>nul
timeout /t 2 >nul

REM Remove previous deployment artifacts
if exist deployment_package rmdir /s /q deployment_package 2>nul
if exist *.zip del /f *.zip 2>nul

REM Clean all bin/obj folders
for /d /r . %%d in (bin,obj) do @if exist "%%d" rd /s /q "%%d" 2>nul

REM Clean dotnet build cache
dotnet clean --configuration Release --verbosity quiet >nul 2>&1
dotnet nuget locals all --clear >nul 2>&1

REM Clear Razor/Roslyn cache
if exist "%TEMP%\Razor" rmdir /s /q "%TEMP%\Razor" 2>nul
if exist "%LOCALAPPDATA%\Microsoft\VisualStudio\RoslynCache" rmdir /s /q "%LOCALAPPDATA%\Microsoft\VisualStudio\RoslynCache" 2>nul

echo      [OK] Environment cleaned successfully
echo.

REM ------------------------------------------
REM Step 2: Download curl.exe (for air-gapped systems)
REM ------------------------------------------
echo [2/8] Downloading curl.exe for air-gapped deployment...

REM Create temp directory for curl download
if not exist "temp_curl" mkdir temp_curl

REM Check if we already have curl.exe in temp directory
if exist "temp_curl\curl.exe" (
    echo      [OK] Using cached curl.exe
) else (
    echo      Downloading curl.exe from official source...
    
    REM Try to download using PowerShell (works on most Windows systems)
    powershell -Command "& { try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; $ProgressPreference = 'SilentlyContinue'; Invoke-WebRequest -Uri 'https://curl.se/windows/dl-8.11.0_1/curl-8.11.0_1-win64-mingw.zip' -OutFile 'temp_curl\curl.zip' -UseBasicParsing; Expand-Archive -Path 'temp_curl\curl.zip' -DestinationPath 'temp_curl' -Force; Copy-Item 'temp_curl\curl-8.11.0_1-win64-mingw\bin\curl.exe' -Destination 'temp_curl\curl.exe' -Force; Remove-Item 'temp_curl\curl.zip' -Force; Remove-Item 'temp_curl\curl-8.11.0_1-win64-mingw' -Recurse -Force; Write-Host '     [OK] curl.exe downloaded successfully' } catch { Write-Host '     [WARNING] Could not download curl.exe - will check system curl' } }" 2>nul
    
    REM If download failed, try to copy from system
    if not exist "temp_curl\curl.exe" (
        echo      [INFO] Download failed, attempting to use system curl...
        where curl.exe >nul 2>&1
        if !errorlevel! equ 0 (
            for /f "delims=" %%i in ('where curl.exe') do (
                copy "%%i" "temp_curl\curl.exe" >nul 2>&1
                echo      [OK] Copied system curl.exe
                goto curl_found
            )
        )
        echo      [WARNING] curl.exe not available - deployment will rely on system curl
        echo      [WARNING] Air-gapped systems may need manual curl installation
    ) else (
        echo      [OK] curl.exe ready for deployment
    )
)
:curl_found

echo.

REM ------------------------------------------
REM Step 3: Restore Dependencies
REM ------------------------------------------
echo [3/8] Restoring NuGet packages...

dotnet restore --verbosity quiet
if %ERRORLEVEL% NEQ 0 (
    echo      [ERROR] Package restore failed
    pause
    exit /b 1
)

echo      [OK] Packages restored successfully
echo.

REM ------------------------------------------
REM Step 4: Build Application (Self-Contained)
REM ------------------------------------------
echo [4/8] Building self-contained application...

dotnet publish ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output deployment_package\Application ^
    --verbosity minimal ^
    -p:PublishSingleFile=false ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:PublishTrimmed=false ^
    -p:PublishReadyToRun=true ^
    -p:EnvironmentName=Production ^
    > build.log 2>&1

if %ERRORLEVEL% NEQ 0 (
    echo      [ERROR] Build failed. Check build.log for details
    type build.log
    pause
    exit /b 1
)

echo      [OK] Build completed successfully
echo.

REM ------------------------------------------
REM Step 5: Create Package Structure
REM ------------------------------------------
echo [5/8] Creating deployment package structure...

REM Create folder structure
mkdir deployment_package\Scripts 2>nul
mkdir deployment_package\ServiceScripts 2>nul
mkdir deployment_package\Documentation 2>nul
mkdir deployment_package\Tools 2>nul

echo      [OK] Folders created
echo.

REM ------------------------------------------
REM Step 6: Copy Service Management Scripts and Tools
REM ------------------------------------------
echo [6/8] Copying service management scripts and tools...

REM Copy curl.exe to Tools folder if available
if exist "temp_curl\curl.exe" (
    copy /Y "temp_curl\curl.exe" deployment_package\Tools\ >nul
    echo      [OK] curl.exe copied to Tools folder
) else (
    echo      [WARNING] curl.exe not available for packaging
)

REM Copy Windows Service installation scripts
if exist install_service.bat (
    copy /Y install_service.bat deployment_package\ServiceScripts\ >nul
    echo      [OK] install_service.bat
)
if exist uninstall_service.bat (
    copy /Y uninstall_service.bat deployment_package\ServiceScripts\ >nul
    echo      [OK] uninstall_service.bat
)
if exist manage_service.bat (
    copy /Y manage_service.bat deployment_package\ServiceScripts\ >nul
    echo      [OK] manage_service.bat
)
if exist check_service_status.bat (
    copy /Y check_service_status.bat deployment_package\ServiceScripts\ >nul
    echo      [OK] check_service_status.bat
)

REM Create smart SCADA integration script with bundled curl support
REM This replaces both the old standard and bundled versions
(
echo @echo off
echo setlocal
echo :: SCADA SMS Integration Script - Smart curl Detection
echo :: Tries bundled curl first, falls back to system curl
echo :: Works on both air-gapped and modern Windows systems
echo if "%%~1"=="" exit /b 1
echo if "%%~2"=="" exit /b 1
echo set "MSG=%%~1"
echo set "GRP=%%~2" 
echo set "VALUE=%%~3"
echo set "ALM=%%~4"
echo set "PRI=%%~5"
echo if not defined ALM set "ALM=SCADA-%%RANDOM%%"
echo if not defined PRI set "PRI=normal"
echo echo %%GRP%%^| findstr /r "^[0-9]*$" ^>nul ^|^| exit /b 1
echo.
echo :: Append VALUE parameter to message if provided
echo if defined VALUE ^(
echo     set "MSG=%%MSG%% - %%VALUE%%"
echo ^)
echo set "MSG=%%MSG:"=\"%%"
echo.
echo :: Smart curl detection: try bundled first, then system
echo set "CURL_PATH=%%~dp0..\Tools\curl.exe"
echo if not exist "%%CURL_PATH%%" set "CURL_PATH=curl.exe"
echo.
echo "%%CURL_PATH%%" -X POST http://localhost:5000/api/sms/send -H "Content-Type: application/json" -d "{\"message\":\"%%MSG%%\",\"groupId\":%%GRP%%,\"alarmId\":\"%%ALM%%\",\"priority\":\"%%PRI%%\"}" --silent --connect-timeout 5 --max-time 15 ^>nul 2^>^&1
echo exit /b %%errorlevel%%
) > deployment_package\Scripts\Scada_sms.bat
echo      [OK] Scada_sms.bat (smart curl detection)

REM Create quick restart script
(
echo @echo off
echo title SCADA SMS System - Quick Restart
echo echo.
echo echo Restarting SCADA SMS System service...
echo echo.
echo sc stop SCADASMSSystem
echo timeout /t 3 /nobreak ^>nul
echo sc start SCADASMSSystem
echo echo.
echo echo Service restarted!
echo timeout /t 2
) > deployment_package\Scripts\restart_service.bat
echo      [OK] restart_service.bat

REM Create status checker
(
echo @echo off
echo title SCADA SMS System - Status
echo echo.
echo sc query SCADASMSSystem
echo echo.
echo pause
) > deployment_package\Scripts\check_status.bat
echo      [OK] check_status.bat

echo.

REM ------------------------------------------
REM Step 7: Create Documentation
REM ------------------------------------------
echo [7/8] Creating deployment documentation...

REM Copy existing documentation
if exist WINDOWS_SERVICE_GUIDE.md (
    copy /Y WINDOWS_SERVICE_GUIDE.md deployment_package\Documentation\ >nul
    echo      [OK] WINDOWS_SERVICE_GUIDE.md
)
if exist SERVICE_INSTALLATION_SUMMARY.md (
    copy /Y SERVICE_INSTALLATION_SUMMARY.md deployment_package\Documentation\ >nul
    echo      [OK] SERVICE_INSTALLATION_SUMMARY.md
)

REM Create simple text README (no PowerShell issues)
(
echo SCADA SMS System - Production Deployment Package
echo.
echo Build Information:
echo   Build Date: %DATE% %TIME%
echo   Build Machine: %COMPUTERNAME%
echo   Target: Windows x64 Self-Contained
echo   Framework: .NET 9
echo   Includes: curl.exe for air-gapped systems
echo.
echo Package Contents:
echo   - Application folder: Main application files
echo   - ServiceScripts folder: Windows Service management
echo   - Scripts folder: SCADA integration and utilities
echo   - Tools folder: curl.exe and other utilities
echo   - Documentation folder: Installation guides
echo.
echo Quick Start:
echo   1. Edit Application\appsettings.json
echo   2. Run ServiceScripts\install_service.bat as Administrator
echo   3. Copy Scripts\Scada_sms.bat to SCADA PC
echo      (Smart curl detection - works with bundled or system curl)
echo   4. Verify: http://localhost:5000
echo.
echo Service Management:
echo   - Install: ServiceScripts\install_service.bat
echo   - Manage: ServiceScripts\manage_service.bat
echo   - Status: ServiceScripts\check_service_status.bat
echo   - Uninstall: ServiceScripts\uninstall_service.bat
echo.
echo SCADA Integration:
echo   - Script: Scripts\Scada_sms.bat
echo   - Usage: Scada_sms.bat "message" groupId "value"
echo   - Auto-detects: Uses bundled curl.exe or system curl
echo   - Air-gapped ready: Works without system curl
echo.
echo curl.exe Information:
echo   - Location: Tools\curl.exe
echo   - Auto-detection: Script tries bundled first, then system
echo   - No configuration needed: Works automatically
echo.
echo Monitoring:
echo   - Logs: C:\SCADA\Logs\
echo   - Web UI: http://localhost:5000
echo   - Health: http://localhost:5000/health
echo.
echo For detailed instructions, see Documentation folder.
) > deployment_package\README.txt

echo      [OK] README.txt created
echo.

REM Create VERSION file
(
echo ============================================
echo  SCADA SMS System - Version Information
echo ============================================
echo.
echo Build Date: %DATE% %TIME%
echo Build Machine: %COMPUTERNAME%
echo Build User: %USERNAME%
echo.
echo Framework: .NET 9
echo Runtime: Windows x64 Self-Contained
echo Includes: curl.exe v8.11.0 ^(bundled^)
echo.
echo Features:
echo  - Windows Service Support: YES
echo  - SCADA Integration: YES
echo  - Auto-Start: YES
echo  - Auto-Restart: YES
echo  - Web Dashboard: YES
echo  - SMS Audit: YES
echo  - Health Monitoring: YES
echo  - Air-Gapped Support: YES ^(curl.exe included^)
echo  - Smart curl Detection: YES
echo.
echo Components:
echo  - Application: SCADASMSSystem.Web.exe
echo  - Service Scripts: 4 files
echo  - SCADA Script: Scada_sms.bat ^(smart curl detection^)
echo  - curl.exe: v8.11.0 in Tools folder
echo  - Documentation: 2 guides
echo.
echo Air-Gapped Deployment:
echo  - All dependencies included
echo  - No internet required after deployment
echo  - curl.exe bundled for SMS communication
echo  - Automatic curl detection and fallback
echo.
echo ============================================
) > deployment_package\VERSION.txt

echo      [OK] VERSION.txt created

REM Create Tools README
(
echo ============================================
echo  SCADA SMS System - Tools Folder
echo ============================================
echo.
echo This folder contains utility tools for air-gapped systems.
echo.
echo Contents:
echo   - curl.exe: HTTP client for SMS API communication
echo.
echo curl.exe Information:
echo   Version: 8.11.0
echo   Purpose: HTTP requests to SMS service
echo   Used by: Scada_sms_bundled.bat
echo   License: MIT/curl license
echo   Source: https://curl.se/
echo.
echo Usage:
echo   The Scada_sms_bundled.bat script automatically uses this
echo   curl.exe if available, or falls back to system curl.
echo.
echo For air-gapped SCADA systems:
echo   1. Copy the entire deployment package
echo   2. Use Scada_sms_bundled.bat instead of Scada_sms.bat
echo   3. The bundled curl.exe will be used automatically
echo.
echo Manual curl usage:
echo   curl.exe -X POST http://localhost:5000/api/sms/send ^
echo     -H "Content-Type: application/json" ^
echo     -d "{\"message\":\"test\"}"
echo.
echo ============================================
) > deployment_package\Tools\README.txt

if exist "temp_curl\curl.exe" (
    echo      [OK] Tools\README.txt created
) else (
    echo      [INFO] Tools\README.txt created (curl.exe not available)
)

echo.

REM ------------------------------------------
REM Step 8: Create ZIP Archive
REM ------------------------------------------
echo [8/8] Creating ZIP archive...

set "TIMESTAMP=%DATE:~-4%%DATE:~3,2%%DATE:~0,2%_%TIME:~0,2%%TIME:~3,2%"
set "TIMESTAMP=%TIMESTAMP: =0%"
set "ZIPNAME=SCADASMSSystem_ServiceDeploy_%TIMESTAMP%.zip"

powershell -ExecutionPolicy Bypass -Command "Compress-Archive -Path 'deployment_package\*' -DestinationPath '%ZIPNAME%' -Force"

if %ERRORLEVEL% NEQ 0 (
    echo      [ERROR] Failed to create ZIP archive
    pause
    exit /b 1
)

echo      [OK] ZIP archive created successfully
echo.

REM ------------------------------------------
REM Build Summary
REM ------------------------------------------
echo ============================================================================
echo  Deployment Package Complete!
echo ============================================================================
echo.

if exist "%ZIPNAME%" (
    for %%I in ("%ZIPNAME%") do (
        echo  Package: %%~nxI
        echo  Size: %%~zI bytes
    )
)

echo.
echo Package Contents:
echo    - Application files (self-contained)
echo    - Windows Service scripts (install, uninstall, manage)
echo    - SCADA integration script (Scada_sms.bat with smart curl detection)
if exist "temp_curl\curl.exe" (
    echo    - curl.exe v8.11.0 (for air-gapped systems)
) else (
    echo    - curl.exe NOT INCLUDED (download failed - manual installation needed)
)
echo    - Complete documentation
echo    - Service management utilities
echo.
echo ============================================================================
echo  Next Steps
echo ============================================================================
echo.
echo  1. Extract ZIP on target server
echo  2. Edit Application\appsettings.json
echo  3. Run ServiceScripts\install_service.bat (as Administrator)
echo  4. Copy Scripts\Scada_sms.bat to SCADA PC
echo     (Works automatically with bundled or system curl)
echo  5. Verify: http://localhost:5000
echo.
if not exist "temp_curl\curl.exe" (
    echo  [IMPORTANT] curl.exe was not downloaded during build.
    echo  For air-gapped systems, you may need to manually include curl.exe
    echo  in the Tools folder or ensure curl is installed on the target system.
    echo.
)
echo  For detailed instructions, see README.txt in the package
echo.
echo ============================================================================
echo.
echo Build completed successfully!
echo.

REM Open deployment folder
explorer deployment_package

pause
