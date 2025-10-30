@echo off
REM =====================================================================
REM SCADA SMS System - Build and Publish for Windows Service
REM =====================================================================
REM This script builds and publishes the application ready for 
REM Windows Service installation
REM 
REM Usage: Run this script before installing as a service
REM =====================================================================

echo.
echo =====================================================================
echo  SCADA SMS System - Build and Publish for Service Deployment
echo =====================================================================
echo.

SET OUTPUT_DIR=.\publish\service
SET PROJECT_FILE=SCADASMSSystem.Web.csproj

echo [1/4] Cleaning previous build...
if exist "%OUTPUT_DIR%" (
    rmdir /s /q "%OUTPUT_DIR%"
    echo     ? Previous build cleaned
) else (
    echo     ? No previous build found
)
echo.

echo [2/4] Restoring NuGet packages...
dotnet restore "%PROJECT_FILE%"
if %errorLevel% neq 0 (
    echo.
    echo ERROR: Failed to restore packages
    pause
    exit /b 1
)
echo     ? Packages restored
echo.

echo [3/4] Building application...
dotnet build "%PROJECT_FILE%" --configuration Release --no-restore
if %errorLevel% neq 0 (
    echo.
    echo ERROR: Build failed
    pause
    exit /b 1
)
echo     ? Build successful
echo.

echo [4/4] Publishing for Windows Service deployment...
dotnet publish "%PROJECT_FILE%" ^
    --configuration Release ^
    --output "%OUTPUT_DIR%" ^
    --runtime win-x64 ^
    --self-contained false ^
    /p:PublishSingleFile=false ^
    /p:EnvironmentName=Production

if %errorLevel% neq 0 (
    echo.
    echo ERROR: Publish failed
    pause
    exit /b 1
)
echo     ? Publish successful
echo.

echo =====================================================================
echo  Build Complete!
echo =====================================================================
echo.
echo Output location: %OUTPUT_DIR%
echo.
echo Next steps:
echo   1. Copy the install scripts to the publish directory:
echo      - install_service.bat
echo      - uninstall_service.bat
echo      - manage_service.bat
echo.
echo   2. Copy the publish directory to your target server
echo.
echo   3. Configure appsettings.json with your settings:
echo      - Database connection string
echo      - SMS API configuration
echo      - Logging preferences
echo.
echo   4. Run install_service.bat as Administrator on the target server
echo.
echo =====================================================================
echo.

REM Copy installation scripts to publish directory
echo Copying service management scripts to publish directory...
copy /Y install_service.bat "%OUTPUT_DIR%\"
copy /Y uninstall_service.bat "%OUTPUT_DIR%\"
copy /Y manage_service.bat "%OUTPUT_DIR%\"
echo     ? Scripts copied
echo.

REM Create a README in the publish directory
echo Creating deployment README...
(
echo SCADA SMS System - Windows Service Deployment Package
echo =====================================================================
echo.
echo This directory contains the SCADA SMS System ready for Windows Service installation.
echo.
echo INSTALLATION STEPS:
echo.
echo 1. Ensure .NET 9 Runtime is installed on the target server
echo    Download from: https://dotnet.microsoft.com/download/dotnet/9.0
echo.
echo 2. Configure appsettings.json with your environment settings:
echo    - ConnectionStrings:DefaultConnection  (SQL Server connection^)
echo    - SmsSettings:ApiUrl                   (SMS API endpoint^)
echo    - SmsSettings:Username                 (SMS API credentials^)
echo    - SmsSettings:Password                 (SMS API credentials^)
echo.
echo 3. Ensure SQL Server is accessible and create the database if needed
echo    The application will auto-create tables on first run.
echo.
echo 4. Run install_service.bat as Administrator to install the service
echo.
echo 5. The service will start automatically and run in the background
echo.
echo MANAGEMENT:
echo.
echo Use manage_service.bat for easy service management, or use these commands:
echo.
echo   Start service:      sc start SCADASMSSystem
echo   Stop service:       sc stop SCADASMSSystem
echo   Check status:       sc query SCADASMSSystem
echo   Uninstall service:  sc delete SCADASMSSystem
echo.
echo LOGS:
echo.
echo Application logs are written to: C:\SCADA\Logs\scada-sms-*.log
echo Windows Event Log: Application log, source "SCADA SMS System"
echo.
echo WEB INTERFACE:
echo.
echo If configured, the web interface will be available at:
echo   http://localhost:5000
echo   https://localhost:5001
echo.
echo Configure URLs in appsettings.json under "Kestrel:Endpoints"
echo.
echo TROUBLESHOOTING:
echo.
echo If service fails to start:
echo 1. Check Event Viewer -^> Application logs
echo 2. Verify database connection string
echo 3. Ensure SQL Server is running and accessible
echo 4. Check file logs in C:\SCADA\Logs\
echo 5. Verify .NET 9 Runtime is installed
echo.
echo For support, check the project documentation or contact your system administrator.
echo.
echo =====================================================================
) > "%OUTPUT_DIR%\DEPLOYMENT_README.txt"
echo     ? README created
echo.

echo Opening publish directory...
explorer "%OUTPUT_DIR%"

echo.
echo =====================================================================
echo  Ready for deployment!
echo =====================================================================
echo.
pause
