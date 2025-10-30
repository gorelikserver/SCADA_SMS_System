@echo off
echo ===============================================
echo Mock SMS API - Quick Verification Test
echo ===============================================
echo.

set SERVER_URL=http://localhost:5555

echo ?? Testing Mock SMS API Server...
echo.

:: Test 1: Health Check
echo [1/4] Health Check...
curl -s -f "%SERVER_URL%/api/sms/health" >nul
if %ERRORLEVEL% equ 0 (
    echo ? Server is healthy
) else (
    echo ? Server health check failed
    echo Is the server running at %SERVER_URL%?
    pause
    exit /b 1
)

:: Test 2: Swagger UI Check
echo [2/4] Swagger UI Check...
curl -s -f "%SERVER_URL%/swagger/index.html" >nul
if %ERRORLEVEL% equ 0 (
    echo ? Swagger UI is accessible
) else (
    echo ? Swagger UI not accessible
)

:: Test 3: API Endpoint Test
echo [3/4] API Endpoint Test...
curl -s -X POST "%SERVER_URL%/api/sms/send" ^
     -H "Content-Type: application/json" ^
     -d "{\"message\":\"Test\",\"groupId\":1}" >nul
if %ERRORLEVEL% equ 0 (
    echo ? API endpoint responding
) else (
    echo ? API endpoint test failed
)

:: Test 4: Status Check
echo [4/4] Status Check...
curl -s -f "%SERVER_URL%/api/sms/status" >nul
if %ERRORLEVEL% equ 0 (
    echo ? Status endpoint working
) else (
    echo ? Status endpoint failed
)

echo.
echo ===============================================
echo ? Mock SMS API Server Verification Complete!
echo ===============================================
echo.
echo ?? Access Points:
echo    Server:     %SERVER_URL%
echo    Swagger UI: %SERVER_URL%/swagger
echo    Health:     %SERVER_URL%/api/sms/health
echo.
echo Ready for testing! ??
echo.
pause