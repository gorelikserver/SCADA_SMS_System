# Air-Gapped Deployment - Quick Reference

## ?? Package Includes curl.exe

This deployment package is **air-gapped ready** and includes all necessary tools:

### ? What's Included
- **Application**: Self-contained .NET 9 runtime (no .NET installation needed)
- **curl.exe v8.11.0**: Bundled HTTP client for air-gapped systems
- **SCADA Scripts**: Both standard and bundled curl versions
- **All Dependencies**: No internet required after deployment

---

## ?? Deployment on Air-Gapped Systems

### Step 1: Extract Package
```batch
REM Extract entire package to target location
xcopy /E /I deployment_package C:\SCADA\
```

### Step 2: Configure Application
```batch
notepad C:\SCADA\Application\appsettings.json
```

### Step 3: Install Windows Service
```batch
REM Run as Administrator
cd C:\SCADA\ServiceScripts
install_service.bat
```

### Step 4: Deploy SCADA Integration

**Copy to SCADA PC:**
```batch
REM Copy script and bundled curl
copy C:\SCADA\Scripts\Scada_sms.bat \\SCADA-PC\C$\ProgramData\
copy C:\SCADA\Tools\curl.exe \\SCADA-PC\C$\ProgramData\
```

**Or copy entire package to preserve folder structure:**
```batch
xcopy /E /I C:\SCADA \\SCADA-PC\C$\SCADA
```

**Note**: The script automatically detects and uses the bundled curl.exe if available, or falls back to system curl. No configuration needed!

---

## ?? SCADA Integration Scripts

### Scada_sms.bat (Smart curl Detection)
- **Auto-detects** bundled curl in `Tools\` folder
- **Falls back** to system curl if bundled not found
- **Works everywhere**: Air-gapped, modern Windows, any system
- **Zero configuration**: Just copy and use

**Usage:**
```batch
Scada_sms.bat "message" groupId "value"
```

**SCADA Alarm Action:**
```vb
'Run '+getalias('PCIMUTIL')+'Scada_sms.bat'+' "'+GetValue(...)+'" 1 "'+GetValue(...)+'"'
```

**How It Works:**
1. Tries bundled curl: `..\Tools\curl.exe`
2. Falls back to system curl: `curl.exe` from PATH
3. Uses whichever is found first
4. No configuration or environment variables needed

---

## ?? Verification

### Check if curl is Available on Target System
```batch
curl --version
```

**If NOT found:**
```
'curl' is not recognized as an internal or external command
```
? **No problem!** Scada_sms.bat will use bundled curl.exe automatically

**If found:**
```
curl 7.x.x or 8.x.x ...
```
? **Also fine!** Scada_sms.bat can use system curl as fallback

**Either way, the script works automatically!**

### Test Bundled curl
```batch
cd C:\SCADA\Tools
curl.exe --version
```

Expected:
```
curl 8.11.0 (x86_64-w64-mingw32)
Release-Date: 2024-11-06
```

### Test SMS Service
```batch
cd C:\SCADA\Scripts
Scada_sms.bat "Test from air-gapped system" 1 "OK"
```

Check logs:
```batch
notepad C:\SCADA\Logs\scada-sms-*.log
```

---

## ?? Package Structure

```
deployment_package\
?
??? Application\                    # .NET 9 self-contained app
?   ??? SCADASMSSystem.Web.exe    # No .NET installation needed
?   ??? appsettings.json          # Configuration (EDIT THIS)
?   ??? ...
?
??? Scripts\                       # SCADA integration
?   ??? Scada_sms.bat            # Smart curl detection (works everywhere)
?   ??? restart_service.bat
?   ??? check_status.bat
?
??? Tools\                         # Utilities for air-gapped
?   ??? curl.exe                 # v8.11.0 bundled
?   ??? README.txt
?
??? ServiceScripts\                # Windows Service management
?   ??? install_service.bat
?   ??? uninstall_service.bat
?   ??? manage_service.bat
?   ??? check_service_status.bat
?
??? Documentation\                 # Complete guides
    ??? WINDOWS_SERVICE_GUIDE.md
    ??? CURL_BUNDLING_GUIDE.md
    ??? ...
```

---

## ?? Air-Gapped System Requirements

### Minimum Requirements
- ? Windows Server 2016+ or Windows 10+
- ? SQL Server (LocalDB, Express, or Full) - **included in most servers**
- ? Administrator privileges for service installation
- ? **NO internet connection required**
- ? **NO .NET installation required** (self-contained)
- ? **NO curl installation required** (bundled)

### What You DON'T Need
- ? Internet connectivity
- ? .NET 9 Runtime (included)
- ? Visual C++ Redistributables
- ? curl.exe system installation
- ? NuGet package restore
- ? External dependencies

---

## ?? Quick Start (TL;DR)

### 1-Minute Air-Gapped Installation

```batch
REM 1. Extract package
xcopy /E /I deployment_package C:\SCADA\

REM 2. Configure
notepad C:\SCADA\Application\appsettings.json

REM 3. Install service (as Administrator)
cd C:\SCADA\ServiceScripts
install_service.bat

REM 4. Deploy SCADA script
copy C:\SCADA\Scripts\Scada_sms_bundled.bat \\SCADA-PC\C$\ProgramData\
copy C:\SCADA\Tools\curl.exe \\SCADA-PC\C$\ProgramData\

REM 5. Test
curl http://localhost:5000/health
```

---

## ?? Security Notes

### Bundled curl.exe
- **Source**: Official curl.se Windows builds
- **Version**: 8.11.0 (November 2024)
- **License**: MIT/curl (free to redistribute)
- **Scanned**: Clean binary from official source
- **Integrity**: SHA-256 hash verified during download

### Why curl is Safe
- ? Industry standard HTTP client
- ? Included in Windows 10/11 by default
- ? Open source and audited
- ? Only communicates with localhost:5000
- ? No external internet access from SCADA script

### Firewall Configuration
```batch
REM Allow local service communication
netsh advfirewall firewall add rule name="SCADA SMS Service" dir=in action=allow protocol=TCP localport=5000
```

---

## ?? Support

### Troubleshooting Steps

**Service won't start:**
```batch
REM Check service status
sc query SCADASMSSystem

REM Check logs
notepad C:\SCADA\Logs\scada-sms-*.log

REM Check Event Viewer
eventvwr.msc
```

**curl not found:**
```batch
REM Verify bundled curl exists
dir C:\SCADA\Tools\curl.exe

REM Test bundled curl
C:\SCADA\Tools\curl.exe --version

REM Use bundled script (not standard)
copy C:\SCADA\Scripts\Scada_sms_bundled.bat ...
```

**Database connection issues:**
```batch
REM Test connection
sqlcmd -S localhost -E -Q "SELECT @@VERSION"

REM Check connection string in appsettings.json
notepad C:\SCADA\Application\appsettings.json
```

### Log Locations
- **Application Logs**: `C:\SCADA\Logs\scada-sms-*.log`
- **Windows Event Log**: Event Viewer ? Application ? "SCADA SMS System"
- **Service Status**: `sc query SCADASMSSystem`

### Health Monitoring
- **Web UI**: http://localhost:5000
- **Health Check**: http://localhost:5000/health
- **API Status**: http://localhost:5000/api/sms/status

---

## ?? Configuration Examples

### appsettings.json (Minimal)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=SCADASMSSystem;Trusted_Connection=True"
  },
  "SmsSettings": {
    "ApiUrl": "http://sms-api-server:8080/services/SendMessage.asmx/SendMessagesReturenMessageID",
    "Username": "your_username",
    "Password": "your_password",
    "Enabled": true,
    "ScadaPcimObjectId": 1
  },
  "Logging": {
    "File": {
      "Path": "C:\\SCADA\\Logs\\scada-sms-.log"
    }
  }
}
```

### SCADA Alarm Action (Complete)

```vb
'Run '+getalias('PCIMUTIL')+'Scada_sms_bundled.bat'+' "Alarm: '+GetValue(PFWALARMNG|PCIM!1:(ThisObject).DESCRIPTION)+'" 1 "'+GetValue(PFWALARMNG|PCIM!1:(ThisObject).ALM_VALUE)+'"'
```

**Parameters:**
- `"Alarm: ..."` - Message text
- `1` - Group ID
- `"..."` - Alarm value

---

## ? Pre-Deployment Checklist

Before deploying to air-gapped SCADA system:

- [ ] Package extracted completely
- [ ] `appsettings.json` configured with correct database and SMS API
- [ ] SQL Server accessible (test with `sqlcmd`)
- [ ] Administrator account available for service installation
- [ ] `Tools\curl.exe` exists (3-4 MB file)
- [ ] `Scada_sms_bundled.bat` script ready to copy
- [ ] Firewall rules configured (port 5000)
- [ ] Target SCADA PC identified and accessible
- [ ] SCADA alarm actions planned and documented

---

## ?? Success Indicators

After successful deployment:

? **Service Running**
```batch
sc query SCADASMSSystem
STATE: RUNNING
```

? **Health Check OK**
```batch
curl http://localhost:5000/health
{"status":"Healthy"}
```

? **Web UI Accessible**
- Navigate to: http://localhost:5000
- See dashboard with system status

? **Test SMS Works**
```batch
Scada_sms_bundled.bat "Test message" 1 "OK"
REM Check logs for successful delivery
```

? **Logs Being Written**
```batch
dir C:\SCADA\Logs\
REM Should see scada-sms-YYYYMMDD.log files
```

---

**Package Version**: 2.1  
**Build Date**: [Auto-generated]  
**Includes**: curl.exe v8.11.0  
**Target**: Windows x64 Air-Gapped Systems  
**License**: Proprietary (SCADA SMS System), MIT (curl.exe)
