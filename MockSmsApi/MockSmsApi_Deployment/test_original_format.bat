@echo off
setlocal enabledelayedexpansion

echo.
echo ===============================================
echo Mock SMS API - ORIGINAL FORMAT Testing
echo ===============================================
echo.
echo Testing with EXACT original SMS provider format:
echo   - POST application/x-www-form-urlencoded
echo   - Parameters: SendToPhoneNumbers, Message, UserName, Password, SenderName
echo.

set MOCK_URL=http://localhost:5555

echo [1/6] Testing Original SMS Provider Format...
curl -s -X POST "%MOCK_URL%/" ^
  -H "Content-Type: application/x-www-form-urlencoded" ^
  -d "SendToPhoneNumbers=+972501234567&Message=Test alarm from SCADA system&UserName=mock_user&Password=mock_password&SenderName=SCADA" > temp_original.txt

if !errorlevel! equ 0 (
    echo ? Original format test successful
    type temp_original.txt
) else (
    echo ? Original format test failed
)
echo.

echo [2/6] Testing with Different Phone Number...
curl -s -X POST "%MOCK_URL%/" ^
  -H "Content-Type: application/x-www-form-urlencoded" ^
  -d "SendToPhoneNumbers=+972507654321&Message=URGENT: Critical equipment failure&UserName=scada_system&Password=your_password&SenderName=SCADA-ALERT" > temp_urgent.txt

if !errorlevel! equ 0 (
    echo ? Urgent message test successful
    type temp_urgent.txt
) else (
    echo ? Urgent message test failed
)
echo.

echo [3/6] Testing Missing Parameters (Error Case)...
curl -s -X POST "%MOCK_URL%/" ^
  -H "Content-Type: application/x-www-form-urlencoded" ^
  -d "UserName=test&Password=test" > temp_error.txt

if !errorlevel! equ 0 (
    echo ? Error handling test completed
    echo Expected error response:
    type temp_error.txt
) else (
    echo ? Error handling test failed
)
echo.

echo [4/6] Testing Modern API Endpoint...
curl -s -X POST "%MOCK_URL%/api/sms/send-original" ^
  -H "Content-Type: application/x-www-form-urlencoded" ^
  -d "SendToPhoneNumbers=+972501234567&Message=Test via modern endpoint&UserName=mock_user&Password=mock_password&SenderName=SCADA" > temp_modern.txt

if !errorlevel! equ 0 (
    echo ? Modern original format endpoint successful
    type temp_modern.txt
) else (
    echo ? Modern endpoint test failed
)
echo.

echo [5/6] Testing Status Endpoint...
curl -s -X GET "%MOCK_URL%/status" > temp_status.txt

if !errorlevel! equ 0 (
    echo ? Status endpoint successful
    type temp_status.txt | jq .
) else (
    echo ? Status endpoint failed
)
echo.

echo [6/6] Testing Health Endpoint...
curl -s -X GET "%MOCK_URL%/api/sms/health" > temp_health.txt

if !errorlevel! equ 0 (
    echo ? Health endpoint successful
    type temp_health.txt | jq .
) else (
    echo ? Health endpoint failed
)
echo.

echo ===============================================
echo ORIGINAL FORMAT TESTING COMPLETE
echo ===============================================
echo.
echo Your SCADA system should now work with the mock server
echo using the exact same API format as your real SMS provider!
echo.
echo Configuration in appsettings.Development.json:
echo   "ApiEndpoint": "http://localhost:5555"
echo   "ApiParams": "{\"SendToPhoneNumbers\": \"phone\", \"Message\": \"message\", \"UserName\": \"username\", \"Password\": \"password\", \"SenderName\": \"sender_name\"}"
echo.

:: Cleanup
del temp_*.txt 2>nul

pause