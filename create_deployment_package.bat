@echo off
REM =====================================================================
REM SCADA SMS System - Complete Deployment Package Creator
REM =====================================================================
REM Creates a ready-to-deploy package with all necessary files
REM =====================================================================

echo.
echo =====================================================================
echo  SCADA SMS System - Deployment Package Creator
echo =====================================================================
echo.

SET OUTPUT_DIR=.\SCADA_SMS_Service_Deployment
SET PUBLISH_DIR=.\publish\service

echo [1/5] Creating deployment package structure...
if exist "%OUTPUT_DIR%" (
    echo Cleaning previous deployment package...
    rmdir /s /q "%OUTPUT_DIR%"
)

mkdir "%OUTPUT_DIR%" 2>nul
mkdir "%OUTPUT_DIR%\Scripts" 2>nul
mkdir "%OUTPUT_DIR%\Documentation" 2>nul

echo     ? Package structure created
echo.

echo [2/5] Building application...
call build_for_service.bat
if %errorLevel% neq 0 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)
echo.

echo [3/5] Copying application files...
xcopy /E /I /Y "%PUBLISH_DIR%\*" "%OUTPUT_DIR%\"
echo     ? Application files copied
echo.

echo [4/5] Organizing deployment package...

REM Move service scripts to Scripts folder
move /Y "%OUTPUT_DIR%\install_service.bat" "%OUTPUT_DIR%\Scripts\" >nul 2>&1
move /Y "%OUTPUT_DIR%\uninstall_service.bat" "%OUTPUT_DIR%\Scripts\" >nul 2>&1
move /Y "%OUTPUT_DIR%\manage_service.bat" "%OUTPUT_DIR%\Scripts\" >nul 2>&1

REM Copy documentation
copy /Y "WINDOWS_SERVICE_GUIDE.md" "%OUTPUT_DIR%\Documentation\" >nul 2>&1
copy /Y "SERVICE_INSTALLATION_SUMMARY.md" "%OUTPUT_DIR%\Documentation\" >nul 2>&1
copy /Y "README.md" "%OUTPUT_DIR%\Documentation\" 2>nul
copy /Y "%OUTPUT_DIR%\DEPLOYMENT_README.txt" "%OUTPUT_DIR%\Documentation\" >nul 2>&1

echo     ? Files organized
echo.

echo [5/5] Creating deployment package README...

(
echo =====================================================================
echo  SCADA SMS System - Windows Service Deployment Package
echo =====================================================================
echo.
echo This package contains everything needed to deploy the SCADA SMS System
echo as a Windows Service that runs automatically in the background.
echo.
echo =====================================================================
echo  QUICK START
echo =====================================================================
echo.
echo 1. PREREQUISITES:
echo    - Windows Server 2016+ or Windows 10+
echo    - .NET 9 Runtime ^(download from: https://dotnet.microsoft.com/download/dotnet/9.0^)
echo    - SQL Server ^(LocalDB, Express, or Full^)
echo    - Administrator privileges
echo.
echo 2. CONFIGURE:
echo    Edit appsettings.json and set:
echo    - ConnectionStrings:DefaultConnection  ^(database connection^)
echo    - SmsSettings:ApiUrl                   ^(SMS API endpoint^)
echo    - SmsSettings:Username and Password    ^(SMS API credentials^)
echo.
echo 3. INSTALL:
echo    Run as Administrator:
echo    Scripts\install_service.bat
echo.
echo 4. VERIFY:
echo    - Check service status: sc query SCADASMSSystem
echo    - Check logs: C:\SCADA\Logs\
echo    - Test health: http://localhost:5000/health
echo    - Access web UI: http://localhost:5000
echo.
echo =====================================================================
echo  PACKAGE CONTENTS
echo =====================================================================
echo.
echo Root Directory:
echo   - SCADASMSSystem.Web.exe         Main application executable
echo   - appsettings.json               Configuration file ^(EDIT THIS^)
echo   - appsettings.Production.json    Production overrides
echo   - *.dll                          Application dependencies
echo.
echo Scripts\:
echo   - install_service.bat            Install as Windows Service
echo   - uninstall_service.bat          Remove the service
echo   - manage_service.bat             Interactive management menu
echo.
echo Documentation\:
echo   - WINDOWS_SERVICE_GUIDE.md       Complete installation guide
echo   - SERVICE_INSTALLATION_SUMMARY.md Quick reference
echo   - DEPLOYMENT_README.txt          Deployment instructions
echo   - README.md                      Project documentation
echo.
echo =====================================================================
echo  INSTALLATION STEPS
echo =====================================================================
echo.
echo Step 1: Install .NET 9 Runtime
echo -----------------------------------------------------------------------
echo Download and install from:
echo https://dotnet.microsoft.com/download/dotnet/9.0
echo.
echo Verify installation:
echo   dotnet --version
echo.
echo Step 2: Configure Database
echo -----------------------------------------------------------------------
echo Edit appsettings.json and set your connection string:
echo.
echo Example for SQL Server:
echo   "Server=localhost;Database=SCADASMSSystem;Integrated Security=true"
echo.
echo Example for LocalDB:
echo   "Server=^(localdb^)\\mssqllocaldb;Database=SCADASMSSystem;Trusted_Connection=True"
echo.
echo The application will automatically create tables on first run.
echo.
echo Step 3: Configure SMS Settings
echo -----------------------------------------------------------------------
echo Edit appsettings.json and set your SMS API configuration:
echo.
echo   "SmsSettings": {
echo     "ApiUrl": "http://your-sms-api:8080/api/sms/send",
echo     "Username": "your_username",
echo     "Password": "your_password",
echo     "Enabled": true
echo   }
echo.
echo Step 4: Install the Service
echo -----------------------------------------------------------------------
echo Run as Administrator:
echo   Scripts\install_service.bat
echo.
echo The service will be:
echo   - Installed as "SCADA SMS Notification System"
echo   - Configured to start automatically on boot
echo   - Configured to restart automatically on failure
echo.
echo Step 5: Verify Installation
echo -----------------------------------------------------------------------
echo Check service status:
echo   sc query SCADASMSSystem
echo.
echo Check logs:
echo   explorer C:\SCADA\Logs\
echo.
echo Test health endpoint:
echo   curl http://localhost:5000/health
echo.
echo Access web interface:
echo   http://localhost:5000
echo.
echo =====================================================================
echo  MANAGEMENT
echo =====================================================================
echo.
echo Interactive Management:
echo   Scripts\manage_service.bat
echo.
echo Command Line:
echo   sc query SCADASMSSystem      ^# Check status
echo   sc start SCADASMSSystem      ^# Start service
echo   sc stop SCADASMSSystem       ^# Stop service
echo.
echo View Logs:
echo   explorer C:\SCADA\Logs\
echo.
echo Windows Services Manager:
echo   services.msc
echo   ^(Look for "SCADA SMS Notification System"^)
echo.
echo =====================================================================
echo  TROUBLESHOOTING
echo =====================================================================
echo.
echo Service won't start:
echo   1. Check Windows Event Viewer ^(eventvwr.msc^)
echo   2. Check application logs in C:\SCADA\Logs\
echo   3. Verify database connection string
echo   4. Ensure SQL Server is running
echo   5. Verify .NET 9 Runtime is installed
echo.
echo Cannot access web interface:
echo   1. Verify service is running: sc query SCADASMSSystem
echo   2. Check if port 5000 is in use: netstat -ano ^| findstr :5000
echo   3. Check firewall settings
echo   4. Review appsettings.json URL configuration
echo.
echo Database errors:
echo   1. Verify connection string
echo   2. Ensure SQL Server is accessible
echo   3. Grant appropriate permissions to service account
echo   4. Check if database exists ^(auto-created if missing^)
echo.
echo =====================================================================
echo  SECURITY NOTES
echo =====================================================================
echo.
echo Default Configuration:
echo   - Service runs under Local System account
echo   - HTTP/HTTPS on ports 5000/5001
echo   - Logs written to C:\SCADA\Logs\
echo.
echo Production Recommendations:
echo   1. Create dedicated service account
echo   2. Grant minimum required permissions
echo   3. Configure firewall rules
echo   4. Use HTTPS only
echo   5. Secure appsettings.json file permissions
echo   6. Enable database connection encryption
echo.
echo To run under specific account:
echo   sc config SCADASMSSystem obj= "DOMAIN\Username" password= "Password"
echo.
echo =====================================================================
echo  MONITORING
echo =====================================================================
echo.
echo Health Check:
echo   http://localhost:5000/health
echo.
echo Application Logs:
echo   C:\SCADA\Logs\scada-sms-*.log
echo   ^(Rotates daily, retains 31 days^)
echo.
echo Windows Event Log:
echo   Event Viewer ^> Application ^> Source: "SCADA SMS System"
echo.
echo Service Status:
echo   sc query SCADASMSSystem
echo.
echo =====================================================================
echo  SUPPORT
echo =====================================================================
echo.
echo Documentation:
echo   See Documentation\ folder for complete guides
echo.
echo Common Commands:
echo   install_service.bat     ^# Install service
echo   uninstall_service.bat   ^# Remove service
echo   manage_service.bat      ^# Interactive management
echo   check_service_status.bat ^# Status report
echo.
echo Log Locations:
echo   Application: C:\SCADA\Logs\
echo   Windows:     Event Viewer ^> Application
echo.
echo Health Endpoint:
echo   http://localhost:5000/health
echo.
echo =====================================================================
echo  VERSION INFORMATION
echo =====================================================================
echo.
echo Package Version: 1.0.0
echo Framework: .NET 9
echo Platform: Windows x64
echo Created: %DATE% %TIME%
echo.
echo =====================================================================
echo.
echo For detailed installation instructions, see:
echo   Documentation\WINDOWS_SERVICE_GUIDE.md
echo.
echo For quick reference, see:
echo   Documentation\SERVICE_INSTALLATION_SUMMARY.md
echo.
echo =====================================================================
) > "%OUTPUT_DIR%\START_HERE.txt"

echo     ? Deployment README created
echo.

REM Create a quick install script in the root
(
echo @echo off
echo echo.
echo echo =====================================================================
echo echo  SCADA SMS System - Quick Install
echo echo =====================================================================
echo echo.
echo echo This will install the SCADA SMS System as a Windows Service.
echo echo.
echo echo BEFORE RUNNING:
echo echo   1. Ensure .NET 9 Runtime is installed
echo echo   2. Configure appsettings.json with your settings
echo echo   3. Ensure you have Administrator privileges
echo echo.
echo pause
echo.
echo cd /d "%%~dp0"
echo call Scripts\install_service.bat
) > "%OUTPUT_DIR%\INSTALL.bat"

echo Created quick installer: INSTALL.bat
echo.

echo =====================================================================
echo  SUCCESS: Deployment package created!
echo =====================================================================
echo.
echo Package location: %OUTPUT_DIR%
echo Package size: 
for /f "tokens=3" %%a in ('dir /-c "%OUTPUT_DIR%" ^| find "File(s)"') do echo   %%a bytes
echo.
echo Package contents:
echo   ? Application files and dependencies
echo   ? Configuration files ^(appsettings.json^)
echo   ? Service installation scripts
echo   ? Complete documentation
echo   ? Quick start guide ^(START_HERE.txt^)
echo   ? One-click installer ^(INSTALL.bat^)
echo.
echo Next steps:
echo   1. Review and edit appsettings.json in the package
echo   2. Copy the entire package to your target server
echo   3. Read START_HERE.txt for installation instructions
echo   4. Run INSTALL.bat as Administrator to install the service
echo.
echo Opening package directory...
explorer "%OUTPUT_DIR%"
echo.
echo =====================================================================
echo.
pause
