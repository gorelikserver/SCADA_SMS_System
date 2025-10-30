@echo off
setlocal enabledelayedexpansion

echo.
echo ===============================================
echo Mock SMS API Server - Comprehensive Test Suite
echo ===============================================
echo.

set MOCK_URL=http://localhost:5555
set API_URL=%MOCK_URL%/api/sms

echo Testing Mock SMS API endpoints...
echo Mock server should be running at: %MOCK_URL%
echo.

:: Test 1: Health Check
echo [1/8] Testing Health Check...
curl -s -X GET "%API_URL%/health" -H "Content-Type: application/json" > temp_health.json
if !errorlevel! equ 0 (
    echo ? Health check successful
    type temp_health.json | jq .
) else (
    echo ? Health check failed
)
echo.

:: Test 2: Service Status
echo [2/8] Testing Service Status...
curl -s -X GET "%API_URL%/status" -H "Content-Type: application/json" > temp_status.json
if !errorlevel! equ 0 (
    echo ? Status check successful
    type temp_status.json | jq .
) else (
    echo ? Status check failed  
)
echo.

:: Test 3: Send Normal SMS
echo [3/8] Testing Send SMS (Normal Priority)...
curl -s -X POST "%API_URL%/send" ^
  -H "Content-Type: application/json" ^
  -d "{\"message\": \"Test alarm from SCADA system\", \"groupId\": 1, \"alarmId\": \"TEST-001\", \"priority\": \"normal\"}" > temp_send1.json
if !errorlevel! equ 0 (
    echo ? Normal SMS send successful
    type temp_send1.json | jq .
) else (
    echo ? Normal SMS send failed
)
echo.

:: Test 4: Send Urgent SMS  
echo [4/8] Testing Send SMS (Urgent Priority)...
curl -s -X POST "%API_URL%/send" ^
  -H "Content-Type: application/json" ^
  -d "{\"message\": \"URGENT: Critical alarm detected!\", \"groupId\": 2, \"alarmId\": \"TEST-002\", \"priority\": \"urgent\"}" > temp_send2.json
if !errorlevel! equ 0 (
    echo ? Urgent SMS send successful
    type temp_send2.json | jq .
) else (
    echo ? Urgent SMS send failed
)
echo.

:: Test 5: Send Test SMS
echo [5/8] Testing Send Test SMS...
curl -s -X POST "%API_URL%/test" ^
  -H "Content-Type: application/json" ^
  -d "{\"message\": \"This is a test message\", \"groupId\": 1}" > temp_test.json
if !errorlevel! equ 0 (
    echo ? Test SMS send successful
    type temp_test.json | jq .
) else (
    echo ? Test SMS send failed
)
echo.

:: Test 6: Legacy Root Endpoint
echo [6/8] Testing Legacy Root Endpoint...
curl -s -X POST "%MOCK_URL%/" ^
  -H "Content-Type: application/json" ^
  -d "{\"message\": \"Legacy endpoint test\", \"groupId\": 3, \"alarmId\": \"LEGACY-001\"}" > temp_legacy.json
if !errorlevel! equ 0 (
    echo ? Legacy endpoint successful
    type temp_legacy.json | jq .
) else (
    echo ? Legacy endpoint failed
)
echo.

:: Test 7: Legacy Status Endpoint
echo [7/8] Testing Legacy Status Endpoint...
curl -s -X GET "%MOCK_URL%/status" ^
  -H "Content-Type: application/json" > temp_legacy_status.json
if !errorlevel! equ 0 (
    echo ? Legacy status successful
    type temp_legacy_status.json | jq .
) else (
    echo ? Legacy status failed
)
echo.

:: Test 8: Error Handling (Invalid Request)
echo [8/8] Testing Error Handling...
curl -s -X POST "%API_URL%/send" ^
  -H "Content-Type: application/json" ^
  -d "{\"message\": \"\", \"groupId\": 0}" > temp_error.json
if !errorlevel! equ 0 (
    echo ? Error handling test completed
    echo Expected error response:
    type temp_error.json | jq .
) else (
    echo ? Error handling test failed
)
echo.

:: Final Status Check
echo ===============================================
echo Final Status Check...
echo ===============================================
curl -s -X GET "%API_URL%/status" -H "Content-Type: application/json" | jq "."
echo.

echo ===============================================
echo Test Suite Complete!
echo ===============================================
echo All endpoints tested. Check responses above for any failures.
echo Mock server stats should show increased message counts.
echo.

:: Cleanup
del temp_*.json 2>nul

echo To integrate with your SCADA system:
echo 1. Make sure appsettings.Development.json points to: %MOCK_URL%
echo 2. Run your SCADA SMS System in Development mode
echo 3. Use the SMS test page to verify integration
echo.

pause