@echo off
echo Testing IAA AFCON SMS Mock Server...
echo.

set MOCK_URL=http://localhost:5555

echo [1] Testing server status...
curl -s "%MOCK_URL%/status"
echo.
echo.

echo [2] Testing SMS endpoint...
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "UserName=d19afcsms" ^
  --data-urlencode "Password=test123" ^
  --data-urlencode "SenderName=IAA Afcon" ^
  --data-urlencode "SendToPhoneNumbers=0546630841" ^
  --data-urlencode "Message=Test message"
echo.
echo.

echo [3] Testing Hebrew message...
curl -s -G "%MOCK_URL%/services/SendMessage.asmx/SendMessagesReturenMessageID" ^
  --data-urlencode "UserName=d19afcsms" ^
  --data-urlencode "Password=test123" ^
  --data-urlencode "SendToPhoneNumbers=0546630841" ^
  --data-urlencode "Message=????? ?????"
echo.

echo.
echo Test complete.
pause