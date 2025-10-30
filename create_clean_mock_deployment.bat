@echo off
echo ===============================================
echo Clean MockSmsApi Deployment Package Creator
echo ===============================================
echo.

echo This script creates a clean deployment package without documentation and redundant files.
echo.

set SOURCE_DIR=MockSmsApi_Clean
set DEPLOY_DIR=MockSmsApi_AirGapped_Deploy

echo Cleaning previous deployment...
rmdir /s /q "%DEPLOY_DIR%" 2>nul

echo Creating deployment directory...
mkdir "%DEPLOY_DIR%"

echo Copying essential files...
copy "%SOURCE_DIR%\Program.cs" "%DEPLOY_DIR%\"
copy "%SOURCE_DIR%\MockSmsApi.csproj" "%DEPLOY_DIR%\"
copy "%SOURCE_DIR%\appsettings.json" "%DEPLOY_DIR%\"
copy "%SOURCE_DIR%\appsettings.Development.json" "%DEPLOY_DIR%\"
copy "%SOURCE_DIR%\start.bat" "%DEPLOY_DIR%\"
copy "%SOURCE_DIR%\start.sh" "%DEPLOY_DIR%\"
copy "%SOURCE_DIR%\build.bat" "%DEPLOY_DIR%\"
copy "%SOURCE_DIR%\test.bat" "%DEPLOY_DIR%\"
copy "%SOURCE_DIR%\README.md" "%DEPLOY_DIR%\"

echo.
echo Building self-contained executable...
cd "%DEPLOY_DIR%"
call build.bat

echo.
echo ===============================================
echo CLEAN DEPLOYMENT PACKAGE READY
echo ===============================================
echo.
echo Location: %DEPLOY_DIR%
echo.
echo Files included:
dir /b "%DEPLOY_DIR%"
echo.
echo Self-contained executable: %DEPLOY_DIR%\publish\MockSmsApi.exe
echo.
echo This package is ready for air-gapped deployment!
echo.

cd ..
pause