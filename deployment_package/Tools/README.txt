============================================
 SCADA SMS System - Tools Folder
============================================

This folder contains utility tools for air-gapped systems.

Contents:
  - curl.exe: HTTP client for SMS API communication

curl.exe Information:
  Version: 8.11.0
  Purpose: HTTP requests to SMS service
  Used by: Scada_sms_bundled.bat
  License: MIT/curl license
  Source: https://curl.se/

Usage:
  The Scada_sms_bundled.bat script automatically uses this
  curl.exe if available, or falls back to system curl.

For air-gapped SCADA systems:
  1. Copy the entire deployment package
  2. Use Scada_sms_bundled.bat instead of Scada_sms.bat
  3. The bundled curl.exe will be used automatically

Manual curl usage:
  curl.exe -X POST http://localhost:5000/api/sms/send echo     -H "Content-Type: application/json" echo     -d "{\"message\":\"test\"}"

============================================
