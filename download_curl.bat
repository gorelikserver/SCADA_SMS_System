@echo off
REM ============================================================================
REM  Download curl.exe for Air-Gapped Systems
REM  
REM  Purpose: Downloads curl.exe for inclusion in deployment packages
REM           when internet access is not available on target systems
REM  
REM  Version: 1.0
REM  Curl Version: 8.11.0
REM ============================================================================

setlocal EnableDelayedExpansion
title Download curl.exe for SCADA SMS System

color 0B

echo.
echo ============================================================================
echo  Download curl.exe for Air-Gapped Deployment
echo ============================================================================
echo.
echo This script downloads curl.exe for inclusion in deployment packages.
echo This is needed for air-gapped Windows systems that don't have curl built-in.
echo.
echo Source: Official curl.se Windows builds
echo Version: 8.11.0
echo License: MIT/curl license (free to redistribute)
echo.
echo ============================================================================
echo.

REM Create temp directory
if not exist "temp_curl" mkdir temp_curl

REM Check if curl already exists
if exist "temp_curl\curl.exe" (
    echo [INFO] curl.exe already exists in temp_curl folder
    echo.
    
    REM Get file size
    for %%I in ("temp_curl\curl.exe") do set size=%%~zI
    echo File: temp_curl\curl.exe
    echo Size: !size! bytes
    echo.
    
    choice /C YN /M "Do you want to re-download curl.exe"
    if errorlevel 2 (
        echo.
        echo Keeping existing curl.exe
        goto :END
    )
    echo.
    echo Re-downloading curl.exe...
)

echo [1/3] Downloading curl.exe from official source...
echo       URL: https://curl.se/windows/dl-8.11.0_1/curl-8.11.0_1-win64-mingw.zip
echo.

REM Download using PowerShell
powershell -Command "& { $ProgressPreference = 'SilentlyContinue'; try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Write-Host '      Downloading...'; Invoke-WebRequest -Uri 'https://curl.se/windows/dl-8.11.0_1/curl-8.11.0_1-win64-mingw.zip' -OutFile 'temp_curl\curl.zip' -UseBasicParsing; Write-Host '      [OK] Download complete'; } catch { Write-Host '      [ERROR] Download failed:' $_.Exception.Message; exit 1; } }"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Failed to download curl.exe
    echo.
    echo Troubleshooting:
    echo   1. Check internet connectivity
    echo   2. Verify firewall/proxy settings
    echo   3. Try downloading manually from https://curl.se/windows/
    echo.
    pause
    exit /b 1
)

echo.
echo [2/3] Extracting curl.exe from archive...

powershell -Command "& { try { Expand-Archive -Path 'temp_curl\curl.zip' -DestinationPath 'temp_curl' -Force; Write-Host '      [OK] Archive extracted'; } catch { Write-Host '      [ERROR] Extraction failed:' $_.Exception.Message; exit 1; } }"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Failed to extract archive
    pause
    exit /b 1
)

echo.
echo [3/3] Copying curl.exe to temp_curl folder...

REM Find and copy curl.exe
if exist "temp_curl\curl-8.11.0_1-win64-mingw\bin\curl.exe" (
    copy /Y "temp_curl\curl-8.11.0_1-win64-mingw\bin\curl.exe" "temp_curl\curl.exe" >nul
    echo       [OK] curl.exe copied
) else (
    echo       [ERROR] curl.exe not found in expected location
    pause
    exit /b 1
)

REM Clean up temporary files
echo.
echo Cleaning up temporary files...
if exist "temp_curl\curl.zip" del /f "temp_curl\curl.zip"
if exist "temp_curl\curl-8.11.0_1-win64-mingw" rmdir /s /q "temp_curl\curl-8.11.0_1-win64-mingw"
echo       [OK] Cleanup complete

echo.
echo ============================================================================
echo  Download Complete!
echo ============================================================================
echo.

REM Get file info
for %%I in ("temp_curl\curl.exe") do (
    echo File: temp_curl\curl.exe
    echo Size: %%~zI bytes
    echo Date: %%~tI
)

echo.
echo ============================================================================
echo  Next Steps
echo ============================================================================
echo.
echo The curl.exe file is now ready for deployment packaging.
echo.
echo Option 1 - Automatic (Recommended):
echo   Run build.bat and curl.exe will be automatically included
echo   in the deployment package in the Tools folder.
echo.
echo Option 2 - Manual:
echo   Copy temp_curl\curl.exe to your deployment package manually:
echo   - Place in Tools folder of deployment package
echo   - Update Scada_sms.bat to use bundled curl if needed
echo.
echo For air-gapped systems:
echo   - The deployment package will include curl.exe
echo   - Use Scada_sms_bundled.bat which auto-detects bundled curl
echo   - No internet or system curl required on target machine
echo.
echo ============================================================================
echo.

REM Test curl.exe
echo Testing curl.exe...
"temp_curl\curl.exe" --version
echo.

if %ERRORLEVEL% EQU 0 (
    echo [OK] curl.exe is working correctly!
) else (
    echo [WARNING] curl.exe test failed - file may be corrupted
)

:END
echo.
echo Press any key to exit...
pause >nul
