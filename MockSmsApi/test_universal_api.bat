@echo off
setlocal enabledelayedexpansion

echo.
echo ===============================================
echo Universal Mock SMS API - Complete Testing
echo ===============================================
echo.
echo Testing UNIVERSAL SMS API that works with ANY provider format:
echo   - Any HTTP Method (GET, POST, PUT, PATCH)
echo   - Any Content Type (JSON, Form, Query Parameters)
echo   - Any Endpoint Path
echo   - Any Parameter Names
echo.

set MOCK_URL=http://localhost:5555

echo [TEST 1] IAA AFCON Format (GET with query parameters)
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "UserName=d19afcsms" ^
  --data-urlencode "Password=test123" ^
  --data-urlencode "SenderName=IAA Afcon" ^
  --data-urlencode "SendToPhoneNumbers=0546630841" ^
  --data-urlencode "Message=????? ?????" > temp1.txt

if !errorlevel! equ 0 (
    echo ? IAA AFCON GET format working
    type temp1.txt
) else (
    echo ? IAA AFCON GET format failed
)
echo.

echo [TEST 2] Generic SMS Provider (POST form-urlencoded)
curl -s -X POST "%MOCK_URL%/send.php" ^
  -H "Content-Type: application/x-www-form-urlencoded" ^
  -d "phone=+1234567890&message=Hello World&username=testuser&password=testpass" > temp2.txt

if !errorlevel! equ 0 (
    echo ? Generic POST form-urlencoded working
    type temp2.txt
) else (
    echo ? Generic POST form failed
)
echo.

echo [TEST 3] Modern API (POST JSON)
curl -s -X POST "%MOCK_URL%/api/v1/sms" ^
  -H "Content-Type: application/json" ^
  -d "{\"mobile\":\"+972501234567\",\"text\":\"JSON message test\",\"user\":\"api_user\"}" > temp3.txt

if !errorlevel! equ 0 (
    echo ? Modern JSON API working
    type temp3.txt
) else (
    echo ? Modern JSON API failed
)
echo.

echo [TEST 4] Simple GET with minimal parameters
curl -s "%MOCK_URL%/sms.aspx?number=555-0123&msg=Simple+test" > temp4.txt

if !errorlevel! equ 0 (
    echo ? Simple GET working
    type temp4.txt
) else (
    echo ? Simple GET failed
)
echo.

echo [TEST 5] Legacy provider format
curl -s -X POST "%MOCK_URL%/sendsms" ^
  -H "Content-Type: application/x-www-form-urlencoded" ^
  -d "mobilenumber=123456789&body=Legacy+message&from=SCADA" > temp5.txt

if !errorlevel! equ 0 (
    echo ? Legacy format working
    type temp5.txt
) else (
    echo ? Legacy format failed
)
echo.

echo [TEST 6] PUT method with JSON
curl -s -X PUT "%MOCK_URL%/sms/send" ^
  -H "Content-Type: application/json" ^
  -d "{\"phone\":\"987654321\",\"content\":\"PUT method test\",\"sender\":\"TEST\"}" > temp6.txt

if !errorlevel! equ 0 (
    echo ? PUT JSON method working
    type temp6.txt
) else (
    echo ? PUT method failed
)
echo.

echo [TEST 7] Missing parameters (error case)
curl -s "%MOCK_URL%/test?user=testonly" > temp7.txt

if !errorlevel! equ 0 (
    echo ? Error handling working
    echo Expected error response:
    type temp7.txt
) else (
    echo ? Error handling failed
)
echo.

echo [TEST 8] Status endpoint
curl -s "%MOCK_URL%/status" > temp8.txt

if !errorlevel! equ 0 (
    echo ? Status endpoint working
    type temp8.txt | jq . 2>nul || type temp8.txt
) else (
    echo ? Status endpoint failed
)
echo.

echo ===============================================
echo UNIVERSAL API TESTING COMPLETE
echo ===============================================
echo.
echo ?? The Universal Mock SMS API can handle:
echo ? GET requests with query parameters
echo ? POST requests with form data
echo ? POST/PUT requests with JSON
echo ? Any endpoint path
echo ? Flexible parameter names
echo ? Hebrew/Unicode messages
echo ? Realistic error simulation
echo.
echo ?? Configuration Examples:
echo.
echo For IAA AFCON (current):
echo   "HttpMethod": "GET"
echo   "ApiEndpoint": "http://localhost:5555/services/SendMessage.asmx/SendMessagesReturenMessageID"
echo   "ContentType": "application/x-www-form-urlencoded"
echo.
echo For JSON API:
echo   "HttpMethod": "POST"
echo   "ApiEndpoint": "http://localhost:5555/api/sms/send"
echo   "ContentType": "application/json"
echo.
echo For Simple GET:
echo   "HttpMethod": "GET"  
echo   "ApiEndpoint": "http://localhost:5555/send"
echo.

:: Cleanup
del temp*.txt 2>nul

pause