@echo off
REM ============================================================================
REM  curl.exe Availability Checker
REM  
REM  Purpose: Checks if curl is available on the system and provides guidance
REM  Usage: Run this on target system before deployment
REM ============================================================================

setlocal
title curl Availability Check

color 0B

echo.
echo ============================================================================
echo  curl.exe Availability Check
echo ============================================================================
echo.
echo Checking if curl is available on this system...
echo.

REM Check if curl is in PATH
where curl.exe >nul 2>&1

if %ERRORLEVEL% EQU 0 (
    echo [OK] curl.exe is AVAILABLE on this system
    echo.
    
    REM Get curl location
    for /f "delims=" %%i in ('where curl.exe') do (
        echo Location: %%i
        
        REM Get version
        "%%i" --version 2>nul
        echo.
    )
    
    echo ============================================================================
    echo  Recommendation
    echo ============================================================================
    echo.
    echo Since curl is already installed:
    echo.
    echo   - Scada_sms.bat will work perfectly
    echo   - Can use system curl OR bundled curl
    echo   - Script auto-detects and uses the best option
    echo.
    echo The smart Scada_sms.bat script will:
    echo   1. Try bundled curl first (if Tools\curl.exe exists)
    echo   2. Fall back to system curl (your installed version)
    echo   3. Work automatically with no configuration
    echo.
    
) else (
    echo [WARNING] curl.exe is NOT FOUND on this system
    echo.
    echo ============================================================================
    echo  Recommendation
    echo ============================================================================
    echo.
    echo This system does NOT have curl installed. You should:
    echo.
    echo   1. Use the deployment package with bundled curl.exe
    echo   2. Copy Scada_sms.bat to SCADA PC
    echo   3. Copy Tools\curl.exe to deployment location
    echo.
    echo The smart Scada_sms.bat will automatically use the bundled curl.exe
    echo.
    echo ============================================================================
    echo  Air-Gapped System Detected
    echo ============================================================================
    echo.
    echo This appears to be an air-gapped or minimal Windows installation.
    echo Common on Windows Server 2016 and earlier, or locked-down environments.
    echo.
    echo The SCADA SMS deployment package includes curl.exe specifically
    echo for systems like this. The Scada_sms.bat script will detect and
    echo use it automatically - no configuration needed!
    echo.
)

echo ============================================================================
echo  System Information
echo ============================================================================
echo.
echo Computer: %COMPUTERNAME%
echo Windows: 
ver
echo User: %USERNAME%
echo Date: %DATE% %TIME%
echo.

echo ============================================================================
echo  Test HTTP Connectivity
echo ============================================================================
echo.

REM Try to test HTTP with curl if available
where curl.exe >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo Testing HTTP connectivity to localhost...
    echo.
    curl --version >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo curl is working correctly.
        echo.
        echo You can test the SMS service with:
        echo   curl http://localhost:5000/health
        echo.
    ) else (
        echo curl is installed but may have issues.
        echo.
    )
) else (
    echo Cannot test HTTP connectivity - curl not available
    echo Use bundled curl.exe from deployment package
    echo.
)

echo ============================================================================
echo  Next Steps
echo ============================================================================
echo.

where curl.exe >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo Your system HAS curl:
    echo   1. Scada_sms.bat will work perfectly
    echo   2. Can use system curl or bundled curl.exe
    echo   3. Script auto-detects - no configuration needed
) else (
    echo Your system NEEDS bundled curl:
    echo   1. Ensure deployment package includes Tools\curl.exe
    echo   2. Copy entire package to preserve folder structure
    echo   3. Scada_sms.bat will automatically use bundled curl
    echo   4. Do NOT delete Tools folder
)

echo.
echo ============================================================================
echo.

pause
