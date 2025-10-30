# Mock SMS API Server - Deployment Builder
# PowerShell script for building deployment package

param(
    [switch]$SelfContained = $false,
    [string]$OutputDir = "MockSmsApi_Deployment",
    [string]$Runtime = "win-x64"
)

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Building Mock SMS API Server for Deployment" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

$PublishDir = Join-Path $OutputDir "MockSmsApi"

Write-Host "?? Building and publishing Mock SMS API Server..." -ForegroundColor Yellow
Write-Host "   Self-contained: $SelfContained" -ForegroundColor Gray
Write-Host "   Runtime: $Runtime" -ForegroundColor Gray
Write-Host "   Output: $OutputDir" -ForegroundColor Gray
Write-Host ""

# Clean previous builds
if (Test-Path $OutputDir) {
    Write-Host "Cleaning previous deployment..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $OutputDir
}

# Create deployment directory
New-Item -ItemType Directory -Path $OutputDir | Out-Null

try {
    # Build and publish the application
    Write-Host "Building Mock SMS API Server..." -ForegroundColor White
    
    if ($SelfContained) {
        dotnet publish MockSmsApi.csproj -c Release -o $PublishDir --self-contained true -r $Runtime
    } else {
        dotnet publish MockSmsApi.csproj -c Release -o $PublishDir --self-contained false
    }
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host ""
    Write-Host "?? Creating deployment package..." -ForegroundColor Yellow
    
    # Copy additional files
    if (Test-Path "README.md") { Copy-Item "README.md" $OutputDir }
    if (Test-Path "start_mock_server.bat") { Copy-Item "start_mock_server.bat" $OutputDir }
    if (Test-Path "test_mock_api.bat") { Copy-Item "test_mock_api.bat" $OutputDir }
    if (Test-Path "test_mock_api.ps1") { Copy-Item "test_mock_api.ps1" $OutputDir }
    
    # Create startup scripts for deployment
    $startBat = @"
@echo off
echo ===============================================
echo Mock SMS API Server Starting...
echo ===============================================
echo.
echo Server will be available at: http://localhost:5555
echo Swagger UI: http://localhost:5555/swagger
echo.
echo Press Ctrl+C to stop the server
echo ===============================================
cd /d "%~dp0MockSmsApi"
dotnet MockSmsApi.dll
pause
"@
    
    $startBat | Out-File -FilePath (Join-Path $OutputDir "start_server.bat") -Encoding ASCII
    
    # Create PowerShell startup script
    $startPs1 = @"
# Mock SMS API Server Startup Script
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Mock SMS API Server Starting..." -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Server will be available at: http://localhost:5555" -ForegroundColor Green
Write-Host "Swagger UI: http://localhost:5555/swagger" -ForegroundColor Green
Write-Host ""
Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Yellow
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

Set-Location (Join-Path `$PSScriptRoot "MockSmsApi")
dotnet MockSmsApi.dll

Write-Host ""
Write-Host "Server stopped." -ForegroundColor Red
Write-Host "Press any key to continue..." -ForegroundColor Gray
`$null = `$Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
"@
    
    $startPs1 | Out-File -FilePath (Join-Path $OutputDir "start_server.ps1") -Encoding UTF8
    
    # Create deployment readme
    $deploymentReadme = @"
# Mock SMS API Server - Deployment Package

## ?? Quick Start

### Windows (Command Prompt)
``````cmd
start_server.bat
``````

### Windows (PowerShell)
``````powershell
.\start_server.ps1
``````

### Cross-Platform
``````bash
cd MockSmsApi
dotnet MockSmsApi.dll
``````

## ?? Access Points

- **API Server**: http://localhost:5555
- **Swagger UI**: http://localhost:5555/swagger
- **Health Check**: http://localhost:5555/api/sms/health
- **Legacy Status**: http://localhost:5555/status

## ?? Available Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `POST /api/sms/send` | POST | Send SMS message |
| `POST /api/sms/test` | POST | Send test SMS |
| `GET /api/sms/status` | GET | Service status |
| `GET /api/sms/health` | GET | Health check |
| `POST /` | POST | Legacy SMS endpoint |
| `GET /status` | GET | Legacy status |

## ?? Requirements

- .NET 9 Runtime $(if ($SelfContained) { "(included with deployment)" } else { "(required on target machine)" })
- Port 5555 available
- Windows, Linux, or macOS

## ?? Features

- ? Identical API endpoints to real SMS service
- ? Swagger UI for interactive testing
- ? Realistic behavior simulation (delays, failures)
- ? Message deduplication
- ? CORS enabled for web testing
- ? No actual SMS messages sent (safe for testing)

## ?? Testing

Use the included test scripts:
- `test_mock_api.bat` (Windows)
- `test_mock_api.ps1` (PowerShell)

Or test manually:
``````bash
# Send SMS
curl -X POST http://localhost:5555/api/sms/send \
  -H "Content-Type: application/json" \
  -d '{"message": "Test alarm", "groupId": 1, "priority": "normal"}'

# Check Status
curl http://localhost:5555/api/sms/status
``````

## ?? Simulation Features

- **Processing Delay**: 100-500ms per message
- **Failure Rate**: 10% of messages fail (configurable)
- **Deduplication**: 5-minute window for duplicate detection
- **Queue Simulation**: Realistic queue size variations
- **Rate Limiting**: Tracked but not enforced

## ?? Important Note

This is a **MOCK SERVER** - no actual SMS messages are sent. Perfect for development, testing, and demonstrations without any real-world SMS costs or deliveries.
"@
    
    $deploymentReadme | Out-File -FilePath (Join-Path $OutputDir "DEPLOYMENT_README.md") -Encoding UTF8
    
    # Create configuration guide
    $configGuide = @"
# Configuration Guide

## Environment Variables

You can configure the mock server using environment variables:

- `ASPNETCORE_URLS`: Server URLs (default: http://localhost:5555)
- `ASPNETCORE_ENVIRONMENT`: Environment (Development/Production)

## Configuration Files

Modify settings in `MockSmsApi/appsettings.json`:

``````json
{
  "MockSmsSettings": {
    "FailureRate": 0.1,        // 10% failure rate
    "MinDelayMs": 100,         // Minimum processing delay
    "MaxDelayMs": 500,         // Maximum processing delay
    "DeduplicationWindowMinutes": 5,
    "RateLimit": 10,
    "MaxQueueSize": 10
  }
}
``````

## Port Configuration

To change the port, modify the `Urls` setting in appsettings.json or use:

``````bash
dotnet MockSmsApi.dll --urls http://localhost:8080
``````
"@
    
    $configGuide | Out-File -FilePath (Join-Path $OutputDir "CONFIGURATION.md") -Encoding UTF8
    
    # Create ZIP package
    Write-Host ""
    Write-Host "?? Creating ZIP package..." -ForegroundColor Yellow
    $zipPath = "$OutputDir.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath }
    
    Compress-Archive -Path "$OutputDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    
    Write-Host ""
    Write-Host "? Deployment package created successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? Package Location:" -ForegroundColor Cyan
    Write-Host "   Folder: $((Get-Item $OutputDir).FullName)" -ForegroundColor White
    Write-Host "   ZIP:    $((Get-Item $zipPath).FullName)" -ForegroundColor White
    Write-Host ""
    Write-Host "?? To deploy on another machine:" -ForegroundColor Cyan
    Write-Host "   1. Copy the ZIP file to target machine" -ForegroundColor White
    Write-Host "   2. Extract the ZIP file" -ForegroundColor White
    Write-Host "   3. Run start_server.bat or start_server.ps1" -ForegroundColor White
    Write-Host "   4. Access Swagger UI at http://localhost:5555/swagger" -ForegroundColor White
    Write-Host ""
    
    Write-Host "?? Package Contents:" -ForegroundColor Cyan
    Get-ChildItem $OutputDir | ForEach-Object { 
        Write-Host "   $($_.Name)" -ForegroundColor White 
    }
    Write-Host ""
    
    # Show package size
    $folderSize = (Get-ChildItem $OutputDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Host "?? Package Size:" -ForegroundColor Cyan
    Write-Host "   Folder: $([math]::Round($folderSize, 2)) MB" -ForegroundColor White
    Write-Host "   ZIP:    $([math]::Round($zipSize, 2)) MB" -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "? Build failed!" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Ready to deploy! ??" -ForegroundColor Green