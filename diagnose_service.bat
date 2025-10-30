@echo off
REM Diagnose why service stops immediately

echo ========================================
echo Service Diagnostic Tool
echo ========================================
echo.

echo [1] Checking if service exists...
sc query SCADASMSSystem
echo.

echo [2] Checking Event Viewer for errors...
echo Opening Event Viewer - Check Application logs for errors from:
echo    - ".NET Runtime"
echo    - "SCADA SMS System"
echo    - Look for errors with timestamp matching service start
echo.
pause
eventvwr.msc
echo.

echo [3] Testing exe directly (to see actual error)...
echo Navigate to application folder and run exe manually:
echo.
echo cd C:\AFCON\deployment_package\Application
echo SCADASMSSystem.Web.exe
echo.
pause

echo [4] Check service configuration...
sc qc SCADASMSSystem
echo.

echo [5] Common Issues:
echo    - Database connection string incorrect
echo    - Port 5000/5001 already in use
echo    - Missing appsettings.json
echo    - Log directory permissions
echo    - Missing DLLs
echo.

pause
