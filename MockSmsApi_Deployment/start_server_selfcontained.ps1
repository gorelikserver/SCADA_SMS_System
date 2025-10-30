# Mock SMS API Server (Self-Contained) - PowerShell Startup
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Mock SMS API Server (Self-Contained)" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This version includes .NET 9 runtime - no installation needed!" -ForegroundColor Green
Write-Host ""
Write-Host "Starting Mock SMS API Server..." -ForegroundColor Yellow
Write-Host "Server will be available at: http://localhost:5555" -ForegroundColor Green
Write-Host "Swagger UI: http://localhost:5555/swagger" -ForegroundColor Green
Write-Host ""
Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Yellow
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

Set-Location (Join-Path $PSScriptRoot "MockSmsApi")

# Check if the executable exists
if (-not (Test-Path "MockSmsApi.exe")) {
    Write-Host "Error: MockSmsApi.exe not found!" -ForegroundColor Red
    Write-Host "Make sure you extracted the complete deployment package." -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# Run the self-contained executable
try {
    .\MockSmsApi.exe
}
catch {
    Write-Host "Error starting server: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Server stopped." -ForegroundColor Red
Read-Host "Press Enter to continue"