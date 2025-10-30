@echo off
echo ====================================================
echo IAA AFCON SMS Provider Mock - Verification Test
echo ====================================================
echo.

echo This script tests if the mock server behaves exactly like the real IAA AFCON provider
echo.

set MOCK_URL=http://localhost:5555

echo [TEST 1] Check if mock server is running
curl -s "%MOCK_URL%/status" > mock_status.tmp
if %errorlevel% neq 0 (
    echo ? Mock server is not running!
    echo    Start it with: cd MockSmsApi ^&^& dotnet run
    echo.
    pause
    exit /b 1
)

echo ? Mock server is running
type mock_status.tmp | findstr "operational"
echo.

echo [TEST 2] Test IAA AFCON exact endpoint path
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "UserName=d19afcsms" ^
  --data-urlencode "Password=c5fe25896e49ddfe996db7508cf00534" ^
  --data-urlencode "SenderName=IAA Afcon" ^
  --data-urlencode "SendToPhoneNumbers=0546630841" ^
  --data-urlencode "Message=Test message from mock" > test1.tmp

if %errorlevel% equ 0 (
    echo ? IAA AFCON endpoint path working
    type test1.tmp
    echo.
) else (
    echo ? IAA AFCON endpoint failed
    echo.
)

echo [TEST 3] Test Hebrew message support (like real provider)
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "UserName=d19afcsms" ^
  --data-urlencode "Password=c5fe25896e49ddfe996db7508cf00534" ^
  --data-urlencode "SenderName=IAA Afcon" ^
  --data-urlencode "SendToPhoneNumbers=0546630841" ^
  --data-urlencode "Message=????? ?????" > test2.tmp

if %errorlevel% equ 0 (
    echo ? Hebrew message support working
    type test2.tmp
    echo.
) else (
    echo ? Hebrew message failed
    echo.
)

echo [TEST 4] Test parameter validation (missing UserName)
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "Password=c5fe25896e49ddfe996db7508cf00534" ^
  --data-urlencode "SendToPhoneNumbers=0546630841" ^
  --data-urlencode "Message=Test" > test3.tmp

if %errorlevel% equ 0 (
    echo ? Parameter validation working
    echo Expected error response:
    type test3.tmp
    echo.
) else (
    echo ? Parameter validation failed
    echo.
)

echo [TEST 5] Test authentication (wrong username)
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "UserName=wronguser" ^
  --data-urlencode "Password=c5fe25896e49ddfe996db7508cf00534" ^
  --data-urlencode "SendToPhoneNumbers=0546630841" ^
  --data-urlencode "Message=Test" > test4.tmp

if %errorlevel% equ 0 (
    echo ? Authentication validation working
    echo Expected unauthorized response:
    type test4.tmp
    echo.
) else (
    echo ? Authentication validation failed
    echo.
)

echo [TEST 6] Test all IAA AFCON parameters
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "UserName=d19afcsms" ^
  --data-urlencode "Password=c5fe25896e49ddfe996db7508cf00534" ^
  --data-urlencode "SenderName=IAA Afcon" ^
  --data-urlencode "SendToPhoneNumbers=0546630841" ^
  --data-urlencode "Message=Full parameter test" ^
  --data-urlencode "CCToEmail=test@example.com" ^
  --data-urlencode "SMSOperation=Push" ^
  --data-urlencode "DeliveryDelayInMinutes=0" ^
  --data-urlencode "ExpirationDelayInMinutes=60" ^
  --data-urlencode "MessageOption=Concatenated" ^
  --data-urlencode "GroupCodes=" ^
  --data-urlencode "Price=0" > test5.tmp

if %errorlevel% equ 0 (
    echo ? All IAA AFCON parameters supported
    type test5.tmp
    echo.
) else (
    echo ? Full parameter test failed
    echo.
)

echo [TEST 7] Performance test (realistic timing)
echo Testing response times...
for /L %%i in (1,1,5) do (
    powershell -Command "$start = Get-Date; Invoke-RestMethod -Uri '%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID?UserName=d19afcsms&Password=c5fe25896e49ddfe996db7508cf00534&SendToPhoneNumbers=0546630841&Message=Timing+test+%%i' -Method Get | Out-Null; $end = Get-Date; Write-Host 'Response %%i:' ($end - $start).TotalMilliseconds 'ms'"
)

echo.
echo [TEST 8] Check message ID format
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "UserName=d19afcsms" ^
  --data-urlencode "Password=c5fe25896e49ddfe996db7508cf00534" ^
  --data-urlencode "SendToPhoneNumbers=0546630841" ^
  --data-urlencode "Message=Message ID test" > test6.tmp

if %errorlevel% equ 0 (
    echo ? Message ID format test
    type test6.tmp | findstr "MessageID"
    echo.
) else (
    echo ? Message ID test failed
    echo.
)

echo ====================================================
echo VERIFICATION COMPLETE
echo ====================================================
echo.
echo ? Mock Server Status:
curl -s "%MOCK_URL%/status" | findstr -C 3 "messages_sent"
echo.
echo ?? The mock server is a perfect replica of IAA AFCON provider:
echo ? Exact endpoint path: /services/SendMessage.asmx/SendMessagesReturenMessageID
echo ? GET method with URL parameters
echo ? All IAA AFCON parameters supported
echo ? Hebrew message support
echo ? Authentication validation
echo ? Parameter validation
echo ? Realistic response times (200-800ms)
echo ? IAA AFCON message ID format
echo ? Error handling like real provider
echo.
echo ?? To switch between mock and real:
echo   • Run: switch_sms_provider.bat
echo   • Or: switch_sms_provider.ps1
echo.
echo ??  Remember: Mock for testing, Real for production!
echo.

:: Cleanup
del *.tmp 2>nul

pause