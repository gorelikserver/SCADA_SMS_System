@echo off
echo Building self-contained deployment package...
echo.

rmdir /s /q publish 2>nul

echo Building for Windows x64...
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish

echo.
echo Build complete. Files are in the 'publish' directory.
echo.
echo To run: publish\MockSmsApi.exe
echo.

pause