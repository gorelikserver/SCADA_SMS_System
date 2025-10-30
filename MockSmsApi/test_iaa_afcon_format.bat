@echo off
setlocal enabledelayedexpansion

echo.
echo ===============================================
echo Mock SMS API - IAA AFCON Format Testing
echo ===============================================
echo.
echo Testing with EXACT IAA AFCON SMS provider format:
echo   - GET /services/SendMessage.asmx/SendMessagesReturenMessageID
echo   - Parameters in URL query string
echo   - Hebrew message support
echo.

set MOCK_URL=http://localhost:5555

echo [1/6] Testing IAA AFCON SMS Provider Format...
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "UserName=d19afcsms" ^
  --data-urlencode "Password=c5fe25896e49ddfe996db7508cf00534" ^
  --data-urlencode "SenderName=IAA Afcon" ^
  --data-urlencode "SendToPhoneNumbers=0546630841" ^
  --data-urlencode "Message=????? 1" ^
  --data-urlencode "CCToEmail=" ^
  --data-urlencode "SMSOperation=Push" ^
  --data-urlencode "DeliveryDelayInMinutes=0" ^
  --data-urlencode "ExpirationDelayInMinutes=60" ^
  --data-urlencode "MessageOption=Concatenated" ^
  --data-urlencode "GroupCodes=" ^
  --data-urlencode "Price=0" > temp_iaa.txt

if !errorlevel! equ 0 (
    echo ? IAA AFCON format test successful
    type temp_iaa.txt
) else (
    echo ? IAA AFCON format test failed
)
echo.

echo [2/6] Testing with Different Phone Number...
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "UserName=d19afcsms" ^
  --data-urlencode "Password=c5fe25896e49ddfe996db7508cf00534" ^
  --data-urlencode "SenderName=IAA Afcon" ^
  --data-urlencode "SendToPhoneNumbers=0507654321" ^
  --data-urlencode "Message=URGENT: ????? ????? ??????" ^
  --data-urlencode "SMSOperation=Push" ^
  --data-urlencode "MessageOption=Concatenated" > temp_urgent.txt

if !errorlevel! equ 0 (
    echo ? Hebrew urgent message test successful
    type temp_urgent.txt
) else (
    echo ? Hebrew message test failed
)
echo.

echo [3/6] Testing Missing Parameters (Error Case)...
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "UserName=test" ^
  --data-urlencode "Password=test" > temp_error.txt

if !errorlevel! equ 0 (
    echo ? Error handling test completed
    echo Expected error response:
    type temp_error.txt
) else (
    echo ? Error handling test failed
)
echo.

echo [4/6] Testing Modern API Endpoint...
curl -s -G "%MOCK_URL%/api/sms/send-iaa-afcon" ^
  --data-urlencode "UserName=d19afcsms" ^
  --data-urlencode "Password=c5fe25896e49ddfe996db7508cf00534" ^
  --data-urlencode "SenderName=IAA Afcon" ^
  --data-urlencode "SendToPhoneNumbers=0546630841" ^
  --data-urlencode "Message=Test via modern endpoint" > temp_modern.txt

if !errorlevel! equ 0 (
    echo ? Modern IAA AFCON endpoint successful
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
    type temp_status.txt
)
echo.

echo [6/6] Testing Health Endpoint...
curl -s -X GET "%MOCK_URL%/api/sms/health" > temp_health.txt

if !errorlevel! equ 0 (
    echo ? Health endpoint successful
    type temp_health.txt | jq .
) else (
    echo ? Health endpoint failed
    type temp_health.txt
)
echo.

echo ===============================================
echo IAA AFCON FORMAT TESTING COMPLETE
echo ===============================================
echo.
echo Your SCADA system should now work with the mock server
echo using the exact same API format as IAA AFCON SMS provider!
echo.
echo Configuration in appsettings.json:
echo   "ApiEndpoint": "http://localhost:5555/services/SendMessage.asmx/SendMessagesReturenMessageID"
echo   "ApiParams": "{\"UserName\": \"username\", \"Password\": \"password\", \"SenderName\": \"sender_name\", \"SendToPhoneNumbers\": \"phone\", \"Message\": \"message\", ...}"
echo.

:: Cleanup
del temp_*.txt 2>nul

pause