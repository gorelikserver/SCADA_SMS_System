@echo off
echo Starting IAA AFCON SMS Mock Server...
echo.

dotnet run --project . --environment Production --urls http://localhost:5555

pause