# ?? SCADA SMS System - Quick Deployment Card

## ? Pre-Flight Checklist

### Build Machine (One-Time Setup)
- [ ] .NET 9 SDK installed (`dotnet --version`)
- [ ] Internet connection available (for curl download)
- [ ] 100 MB free disk space
- [ ] Run `build.bat` successfully

### Target System Requirements
- [ ] Windows Server 2016+ or Windows 10+
- [ ] SQL Server installed (LocalDB/Express/Full)
- [ ] Port 5000 available
- [ ] Administrator access
- [ ] **NO .NET required** (self-contained)
- [ ] **NO curl required** (bundled)

---

## ?? 5-Minute Air-Gapped Deployment

### Step 1: Check curl Availability (30 seconds)
```batch
cd deployment_package
check_curl.bat
```
**?? Note**: Scada_sms.bat works automatically regardless of curl status.  
It tries bundled curl first, then falls back to system curl.

### Step 2: Extract and Configure (2 minutes)
```batch
REM Extract entire package
xcopy /E /I deployment_package C:\SCADA\

REM Edit configuration
notepad C:\SCADA\Application\appsettings.json
```

**Required Settings:**
- `ConnectionStrings.DefaultConnection` ? Your SQL Server
- `SmsSettings.ApiUrl` ? SMS provider API
- `SmsSettings.Username` ? API username
- `SmsSettings.Password` ? API password

### Step 3: Install Windows Service (1 minute)
```batch
REM Run as Administrator
cd C:\SCADA\ServiceScripts
install_service.bat

REM Verify service
sc query SCADASMSSystem
```
**Expected**: `STATE: RUNNING`

### Step 4: Deploy to SCADA PC (1.5 minutes)
```batch
REM Copy script and bundled curl (preserves auto-detection)
copy C:\SCADA\Scripts\Scada_sms.bat \\SCADA-PC\C$\Scripts\
copy C:\SCADA\Tools\curl.exe \\SCADA-PC\C$\Scripts\

REM Or copy entire package to preserve folder structure
xcopy /E /I C:\SCADA \\SCADA-PC\C$\SCADA
```

**Note**: Script automatically uses bundled curl if available, or system curl as fallback.

### Step 5: Test (30 seconds)
```batch
REM Test health
curl http://localhost:5000/health

REM Test SMS (from SCADA PC)
cd C:\Scripts
Scada_sms.bat "Test from SCADA" 1 "OK"

REM Check logs
notepad C:\SCADA\Logs\scada-sms-*.log
```

**Expected**: SMS appears in audit log

**Note**: Scada_sms.bat works automatically - tries bundled curl first, then system curl.

---

## ?? SCADA Alarm Configuration

### Alarm Action Command
```vb
'Run '+getalias('PCIMUTIL')+'Scada_sms.bat'+' "Alarm: '+GetValue(PFWALARMNG|PCIM!1:(ThisObject).DESCRIPTION)+'" 1 "'+GetValue(PFWALARMNG|PCIM!1:(ThisObject).ALM_VALUE)+'"'
```
**Note**: Scada_sms.bat automatically detects and uses bundled or system curl - no configuration needed!

### Parameters
- **Message**: `"Alarm: [DESCRIPTION]"` - Alarm text
- **Group ID**: `1` - Target SMS group (configure in web UI)
- **Value**: `"[ALM_VALUE]"` - Current alarm value

---

## ?? Verification Commands

### Service Status
```batch
sc query SCADASMSSystem
# Expected: STATE: RUNNING

net start | findstr SCADA
# Expected: SCADA SMS Notification System
```

### Health Check
```batch
curl http://localhost:5000/health
# Expected: {"status":"Healthy"}

curl http://localhost:5000
# Expected: HTML dashboard
```

### curl Availability
```batch
REM System curl
curl --version

REM Bundled curl
C:\SCADA\Tools\curl.exe --version

REM Both should show: curl 8.x.x or 7.x.x
```

### Logs
```batch
dir C:\SCADA\Logs\
# Expected: scada-sms-YYYYMMDD.log files

notepad C:\SCADA\Logs\scada-sms-*.log
# Check for startup messages and SMS deliveries
```

---

## ?? Troubleshooting Quick Fixes

### Service Won't Start
```batch
REM Check Event Viewer
eventvwr.msc
# Look in Application log for "SCADA SMS System"

REM Check database connection
sqlcmd -S localhost -E -Q "SELECT @@VERSION"

REM Verify appsettings.json syntax
notepad C:\SCADA\Application\appsettings.json
# Check for missing commas, quotes
```

### curl Not Found
```batch
REM Verify bundled curl exists
dir C:\SCADA\Tools\curl.exe

REM Test bundled curl
C:\SCADA\Tools\curl.exe --version

REM Scada_sms.bat should work automatically with bundled curl
REM If not, check that Tools folder is in correct location relative to Scripts
```

### SMS Not Sending
```batch
REM Check service running
sc query SCADASMSSystem

REM Test API directly
curl -X POST http://localhost:5000/api/sms/send -H "Content-Type: application/json" -d "{\"message\":\"test\",\"groupId\":1}"

REM Check logs
tail C:\SCADA\Logs\scada-sms-*.log

REM Verify group exists and has members
# Open http://localhost:5000/Groups
```

### Web UI Not Accessible
```batch
REM Check port usage
netstat -ano | findstr :5000
# Should show LISTENING

REM Check firewall
netsh advfirewall firewall show rule name="SCADA SMS Service"

REM Add firewall rule if needed
netsh advfirewall firewall add rule name="SCADA SMS Service" dir=in action=allow protocol=TCP localport=5000
```

---

## ?? Success Indicators

### ? All Systems Go
- [x] Service status: **RUNNING**
- [x] Health check: **{"status":"Healthy"}**
- [x] Web UI accessible: **http://localhost:5000**
- [x] Logs being written: **C:\SCADA\Logs\scada-sms-YYYYMMDD.log**
- [x] Test SMS delivered: **Appears in Audit page**
- [x] curl available: **System or bundled**

### ?? Production Ready
```
? Service installed and running
? Database connected and seeded
? SMS API configured and tested
? SCADA script deployed to SCADA PC
? Alarm actions configured
? Logs show successful SMS deliveries
? Web dashboard showing system health
```

---

## ?? Quick Access URLs

| Resource | URL | Description |
|----------|-----|-------------|
| Dashboard | http://localhost:5000 | Main web UI |
| Health Check | http://localhost:5000/health | JSON health status |
| SMS Test | http://localhost:5000/Test/Sms | Send test SMS |
| Groups | http://localhost:5000/Groups | Manage SMS groups |
| Users | http://localhost:5000/Users | Manage users |
| Audit | http://localhost:5000/Audit | SMS delivery history |
| Settings | http://localhost:5000/Settings | System configuration |

---

## ?? Support Resources

### Documentation (In Package)
- `README.txt` - Quick start guide
- `VERSION.txt` - Build information
- `Documentation\WINDOWS_SERVICE_GUIDE.md` - Complete installation
- `Documentation\CURL_BUNDLING_GUIDE.md` - curl.exe details
- `Documentation\AIR_GAPPED_DEPLOYMENT.md` - Air-gapped guide

### Logs and Diagnostics
- **Application Logs**: `C:\SCADA\Logs\scada-sms-*.log`
- **Windows Event Log**: Event Viewer ? Application ? "SCADA SMS System"
- **Service Status**: `sc query SCADASMSSystem`
- **Database**: SQL Server Management Studio

### Common Scripts
- `ServiceScripts\manage_service.bat` - Interactive management
- `ServiceScripts\check_service_status.bat` - Quick status
- `check_curl.bat` - curl availability checker
- `Scripts\restart_service.bat` - Quick restart

---

## ?? Key Features: Smart curl Detection

**Scada_sms.bat automatically:**
1. ? Tries bundled curl first: `..\Tools\curl.exe`
2. ? Falls back to system curl: `curl.exe` from PATH
3. ? Works on air-gapped systems (uses bundled)
4. ? Works on modern Windows (uses system or bundled)
5. ? Zero configuration required
6. ? No environment variables needed

**One script for all scenarios!**

---

## ?? Security Checklist

- [ ] Change default SQL connection string
- [ ] Use strong SMS API password
- [ ] Configure firewall rules for port 5000
- [ ] Run service under dedicated account (not Local System)
- [ ] Enable HTTPS in production
- [ ] Secure appsettings.json file permissions
- [ ] Whitelist `Tools\curl.exe` in antivirus
- [ ] Review and secure log file location
- [ ] Enable database connection encryption
- [ ] Implement regular log rotation

---

## ?? Maintenance Tasks

### Daily
- [ ] Monitor logs for errors: `C:\SCADA\Logs\`
- [ ] Check service status: `sc query SCADASMSSystem`
- [ ] Verify SMS delivery rate: Web UI ? Dashboard

### Weekly
- [ ] Review SMS audit history: Web UI ? Audit
- [ ] Check disk space: `dir C:\SCADA\Logs\`
- [ ] Verify database backup completed

### Monthly
- [ ] Clean old audit records: Web UI ? Settings ? Cleanup Data
- [ ] Review and archive old logs
- [ ] Check for system updates
- [ ] Test disaster recovery procedure

### Quarterly
- [ ] Review user and group configurations
- [ ] Update holiday calendar: Web UI ? Calendar
- [ ] Check for curl.exe updates: https://curl.se/windows/
- [ ] Review and update SMS API credentials

---

## ?? Best Practices

### For Air-Gapped Systems
? Copy entire deployment package (preserves folder structure)  
? Include Tools\curl.exe in deployment  
? Scada_sms.bat works automatically  
? No configuration needed  

### For High Availability
? Monitor service with external tool  
? Configure auto-restart on failure (default)  
? Set up database replication  
? Implement log aggregation  

### For Security
? Limit port 5000 access to local network  
? Use HTTPS in production  
? Rotate SMS API credentials regularly  
? Enable audit logging  

---

**Package Version**: 2.1  
**curl Version**: 8.11.0  
**Platform**: Windows x64  
**Deployment Type**: Self-Contained + Air-Gapped Ready  

---

?? **Questions?** Check `Documentation\` folder for detailed guides  
?? **Issues?** Check logs in `C:\SCADA\Logs\`  
?? **Management?** Run `ServiceScripts\manage_service.bat`
