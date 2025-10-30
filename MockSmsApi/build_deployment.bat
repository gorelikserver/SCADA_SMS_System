@echo off
setlocal

echo ===============================================
echo Building Mock SMS API Server for Deployment
echo ===============================================
echo.

set OUTPUT_DIR=MockSmsApi_Deployment
set PUBLISH_DIR=%OUTPUT_DIR%\MockSmsApi

echo ?? Building and publishing Mock SMS API Server...

:: Clean previous builds
if exist "%OUTPUT_DIR%" (
    echo Cleaning previous deployment...
    rmdir /s /q "%OUTPUT_DIR%"
)

:: Create deployment directory
mkdir "%OUTPUT_DIR%"

:: Build and publish the application
echo Building Mock SMS API Server...
dotnet publish MockSmsApi.csproj -c Release -o "%PUBLISH_DIR%" --self-contained false

if %ERRORLEVEL% neq 0 (
    echo ? Build failed!
    pause
    exit /b 1
)

echo.
echo ?? Creating deployment package...

:: Copy additional files
copy "README.md" "%OUTPUT_DIR%\"
copy "start_mock_server.bat" "%OUTPUT_DIR%\"
copy "test_mock_api.bat" "%OUTPUT_DIR%\"
copy "test_mock_api.ps1" "%OUTPUT_DIR%\"

:: Create startup scripts for deployment
echo @echo off > "%OUTPUT_DIR%\start_server.bat"
echo echo Starting Mock SMS API Server... >> "%OUTPUT_DIR%\start_server.bat"
echo echo Server will be available at: http://localhost:5555 >> "%OUTPUT_DIR%\start_server.bat"
echo echo Swagger UI: http://localhost:5555/swagger >> "%OUTPUT_DIR%\start_server.bat"
echo echo. >> "%OUTPUT_DIR%\start_server.bat"
echo cd /d "%%~dp0MockSmsApi" >> "%OUTPUT_DIR%\start_server.bat"
echo dotnet MockSmsApi.dll >> "%OUTPUT_DIR%\start_server.bat"

:: Create PowerShell startup script
echo # Mock SMS API Server Startup Script > "%OUTPUT_DIR%\start_server.ps1"
echo Write-Host "Starting Mock SMS API Server..." -ForegroundColor Green >> "%OUTPUT_DIR%\start_server.ps1"
echo Write-Host "Server will be available at: http://localhost:5555" -ForegroundColor Yellow >> "%OUTPUT_DIR%\start_server.ps1"
echo Write-Host "Swagger UI: http://localhost:5555/swagger" -ForegroundColor Yellow >> "%OUTPUT_DIR%\start_server.ps1"
echo Write-Host "" >> "%OUTPUT_DIR%\start_server.ps1"
echo Set-Location (Join-Path $PSScriptRoot "MockSmsApi") >> "%OUTPUT_DIR%\start_server.ps1"
echo dotnet MockSmsApi.dll >> "%OUTPUT_DIR%\start_server.ps1"

:: Create deployment readme
echo # Mock SMS API Server - Deployment Package > "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo. >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo ## Quick Start >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo. >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo ### Windows (Command Prompt^) >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo ```cmd >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo start_server.bat >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo ``` >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo. >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo ### Windows (PowerShell^) >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo ```powershell >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo .\start_server.ps1 >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo ``` >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo. >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo ## Access Points >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo. >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo - **API Server**: http://localhost:5555 >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo - **Swagger UI**: http://localhost:5555/swagger >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo - **Health Check**: http://localhost:5555/api/sms/health >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo. >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo ## Requirements >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo. >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo - .NET 9 Runtime (included with deployment^) >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo - Port 5555 available >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo. >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo ## Features >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo. >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo - Identical API endpoints to real SMS service >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo - Swagger UI for interactive testing >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo - Realistic behavior simulation >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"
echo - No actual SMS messages sent >> "%OUTPUT_DIR%\DEPLOYMENT_README.md"

:: Create ZIP package
echo.
echo ?? Creating ZIP package...
if exist "%OUTPUT_DIR%.zip" del "%OUTPUT_DIR%.zip"

powershell -Command "Compress-Archive -Path '%OUTPUT_DIR%\*' -DestinationPath '%OUTPUT_DIR%.zip' -CompressionLevel Optimal"

if %ERRORLEVEL% eq 0 (
    echo.
    echo ? Deployment package created successfully!
    echo.
    echo ?? Package Location:
    echo    Folder: %CD%\%OUTPUT_DIR%
    echo    ZIP:    %CD%\%OUTPUT_DIR%.zip
    echo.
    echo ?? To deploy on another machine:
    echo    1. Copy the ZIP file to target machine
    echo    2. Extract the ZIP file  
    echo    3. Run start_server.bat or start_server.ps1
    echo    4. Access Swagger UI at http://localhost:5555/swagger
    echo.
    echo ?? Package Contents:
    dir /b "%OUTPUT_DIR%"
    echo.
) else (
    echo ? Failed to create ZIP package
    echo Manual deployment folder is available at: %OUTPUT_DIR%
)

echo ===============================================
echo Build Complete!
echo ===============================================
pause