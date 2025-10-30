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

Set-Location (Join-Path $PSScriptRoot "MockSmsApi")
dotnet MockSmsApi.dll

Write-Host ""
Write-Host "Server stopped." -ForegroundColor Red
Write-Host "Press any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
