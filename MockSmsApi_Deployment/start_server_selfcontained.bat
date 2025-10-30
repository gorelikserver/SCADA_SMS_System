@echo off
echo ===============================================
echo Mock SMS API Server (Self-Contained)
echo ===============================================
echo.
echo This version includes .NET 9 runtime - no installation needed!
echo.
echo Starting Mock SMS API Server...
echo Server will be available at: http://localhost:5555
echo Swagger UI: http://localhost:5555/swagger
echo.
echo Press Ctrl+C to stop the server
echo ===============================================
echo.

cd /d "%~dp0MockSmsApi"

:: Check if the executable exists
if not exist "MockSmsApi.exe" (
    echo Error: MockSmsApi.exe not found!
    echo Make sure you extracted the complete deployment package.
    pause
    exit /b 1
)

:: Run the self-contained executable
MockSmsApi.exe

echo.
echo Server stopped.
pause