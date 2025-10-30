@echo off
echo Starting Mock SMS API Server...
echo.
echo This mock server provides the same endpoints as your real SMS service
echo but doesn't send actual SMS messages - perfect for testing!
echo.
echo Available endpoints:
echo   POST /api/sms/send   - Send SMS
echo   POST /api/sms/test   - Send test SMS  
echo   GET  /api/sms/status - Service status
echo   GET  /api/sms/health - Health check
echo   POST /           - Legacy SMS endpoint
echo   GET  /status     - Legacy status endpoint
echo.
echo Swagger UI: http://localhost:5555/swagger
echo.

cd /d "%~dp0"
dotnet run