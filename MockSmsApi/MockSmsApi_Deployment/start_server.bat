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
