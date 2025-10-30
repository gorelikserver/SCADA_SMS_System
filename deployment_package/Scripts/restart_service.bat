@echo off
title SCADA SMS System - Quick Restart
echo.
echo Restarting SCADA SMS System service...
echo.
sc stop SCADASMSSystem
timeout /t 3 /nobreak >nul
sc start SCADASMSSystem
echo.
echo Service restarted
timeout /t 2
