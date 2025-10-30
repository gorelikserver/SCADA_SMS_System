# curl.exe Inclusion for Air-Gapped Systems

## Overview

Starting with version 2.1, the SCADA SMS System deployment package includes `curl.exe` to support air-gapped Windows systems that don't have curl pre-installed.

## Why curl.exe is Needed

### The Problem
- Modern Windows 10/11 systems typically include curl.exe
- **Air-gapped Windows Server systems** (especially older versions) often don't have curl
- Windows Server 2016 and earlier don't include curl by default
- Industrial/SCADA environments are frequently air-gapped for security

### The Solution
The `Scada_sms.bat` script uses curl to make HTTP POST requests to the SMS service API. By bundling curl.exe in the deployment package, we ensure the system works on all Windows environments without requiring:
- Internet connectivity to download curl
- System administrator to install curl separately
- Complex workarounds or alternative HTTP clients

## What's Included

### curl.exe Details
- **Version**: 8.11.0 (latest stable as of build)
- **Source**: https://curl.se/windows/
- **License**: MIT/curl license (free to use and redistribute)
- **Size**: ~4 MB
- **Platform**: Windows x64
- **Location in Package**: `Tools\curl.exe`

### Two SCADA Integration Scripts

#### 1. Scada_sms.bat (Original)
- Uses system curl: `curl.exe`
- Requires curl to be in PATH or Windows\System32
- Best for modern Windows systems with built-in curl

#### 2. Scada_sms_bundled.bat (New - Recommended)
- Tries bundled curl first: `..\Tools\curl.exe`
- Falls back to system curl if bundled not found
- **Recommended for air-gapped systems**
- Automatically uses the right curl

## Build Process

### Automatic Download
The `build.bat` script now includes automatic curl.exe download:

```batch
[2/8] Downloading curl.exe for air-gapped deployment...
```

**What Happens:**
1. Creates `temp_curl` directory
2. Downloads latest curl from https://curl.se/windows/
3. Extracts curl.exe from the archive
4. Copies to `deployment_package\Tools\`
5. Creates both standard and bundled SCADA scripts

**If Download Fails:**
- Tries to copy from system curl.exe
- Warns in build output
- Deployment still succeeds but curl.exe may need manual addition

### Manual Download Option
If you need to download curl.exe separately:

```batch
download_curl.bat
```

This script:
- Downloads curl.exe v8.11.0 from official source
- Saves to `temp_curl\curl.exe`
- Verifies the download
- Next build.bat run will use it automatically

## Deployment Package Structure

```
SCADASMSSystem_ServiceDeploy_YYYYMMDD_HHMM.zip
?
??? Application\                    # .NET application files
?   ??? SCADASMSSystem.Web.exe
?   ??? appsettings.json
?   ??? ...
?
??? Scripts\                        # SCADA integration scripts
?   ??? Scada_sms.bat              # Uses system curl
?   ??? Scada_sms_bundled.bat      # Uses bundled curl (RECOMMENDED)
?   ??? restart_service.bat
?   ??? check_status.bat
?
??? ServiceScripts\                 # Windows Service management
?   ??? install_service.bat
?   ??? uninstall_service.bat
?   ??? manage_service.bat
?   ??? check_service_status.bat
?
??? Tools\                          # Utility tools
?   ??? curl.exe                   # Bundled curl for air-gapped systems
?   ??? README.txt                 # Tools documentation
?
??? Documentation\                  # Installation guides
?   ??? WINDOWS_SERVICE_GUIDE.md
?   ??? SERVICE_INSTALLATION_SUMMARY.md
?
??? README.txt                      # Quick start guide
??? VERSION.txt                     # Build information
```

## Installation Instructions

### For Air-Gapped SCADA Systems

1. **Copy Entire Package**
   ```batch
   xcopy /E /I SCADASMSSystem_ServiceDeploy_* C:\SCADA\
   ```

2. **Configure the Application**
   ```batch
   notepad C:\SCADA\Application\appsettings.json
   ```

3. **Install Windows Service**
   ```batch
   cd C:\SCADA\ServiceScripts
   install_service.bat
   ```

4. **Deploy to SCADA PC**
   - Copy `Scripts\Scada_sms_bundled.bat` to SCADA PC
   - Copy `Tools\curl.exe` to SCADA PC (same directory or accessible path)
   - OR copy entire package to preserve relative paths

5. **Configure SCADA Alarm Actions**
   ```batch
   'Run '+getalias('PCIMUTIL')+'Scada_sms_bundled.bat'+' "+'"+GetValue(...)+'"'+[GROUP_ID]+' "+GetValue(...)+'"'
   ```

### For Systems with curl Installed

If the target system has curl.exe (Windows 10/11, manually installed):

1. Use `Scripts\Scada_sms.bat` instead
2. The bundled `Tools\curl.exe` is not required
3. Smaller deployment if you remove `Tools\` folder

## Verification

### Check if curl is Available

**On Target System:**
```batch
curl --version
```

**If curl is not found:**
```
'curl' is not recognized as an internal or external command
```
? Use `Scada_sms_bundled.bat` with bundled curl.exe

**If curl is found:**
```
curl 7.xx.x (Windows) ...
```
? Can use `Scada_sms.bat` OR `Scada_sms_bundled.bat`

### Test the Bundled curl

```batch
cd deployment_package\Tools
curl.exe --version
```

Expected output:
```
curl 8.11.0 (x86_64-w64-mingw32) ...
Release-Date: 2024-xx-xx
Protocols: ...
Features: ...
```

## SCADA Integration Examples

### Using Bundled curl Script

**Alarm Action Command:**
```vb
'Run '+getalias('PCIMUTIL')+'Scada_sms_bundled.bat'+' "Alarm: '+GetValue(PFWALARMNG|PCIM!1:(ThisObject).DESCRIPTION)+'" 1 "'+GetValue(PFWALARMNG|PCIM!1:(ThisObject).ALM_VALUE)+'"'
```

**Manual Test:**
```batch
cd C:\SCADA\Scripts
Scada_sms_bundled.bat "Test Message" 1 "123.45"
```

### Script Behavior

**Scada_sms_bundled.bat logic:**
```batch
:: Try bundled curl first
set "CURL_PATH=%~dp0..\Tools\curl.exe"
if not exist "%CURL_PATH%" set "CURL_PATH=curl.exe"

"%CURL_PATH%" -X POST http://localhost:5000/api/sms/send ...
```

**Fallback Order:**
1. Bundled curl: `..\Tools\curl.exe` (relative to script location)
2. System curl: `curl.exe` (from PATH)

## Troubleshooting

### curl.exe Not Downloaded During Build

**Symptoms:**
```
[2/8] Downloading curl.exe for air-gapped deployment...
     [WARNING] Could not download curl.exe - will check system curl
```

**Solutions:**
1. **Check Internet Connection**: Ensure build machine has internet access
2. **Manual Download**: Run `download_curl.bat`
3. **Manual Copy**: Download from https://curl.se/windows/ and place in `temp_curl\curl.exe`
4. **Re-run Build**: Next `build.bat` will use the cached curl.exe

### curl Not Found on Target System

**Error Message:**
```
'curl' is not recognized as an internal or external command
```

**Solutions:**
1. **Use Bundled Script**: Switch to `Scada_sms_bundled.bat`
2. **Verify Tools Folder**: Ensure `Tools\curl.exe` exists in deployment
3. **Check Paths**: Verify relative paths are correct
4. **Manual Install**: Install curl system-wide (not recommended for air-gapped)

### Bundled curl Not Found

**Error Message:**
```
'Tools\curl.exe' is not recognized...
```

**Solutions:**
1. **Verify Package Structure**: Ensure `Tools\curl.exe` exists
2. **Check Relative Paths**: Script looks for `..\Tools\curl.exe` from Scripts folder
3. **Re-extract Package**: Ensure complete ZIP extraction
4. **Copy curl.exe**: Manually copy from another deployment or download

### Permission Denied

**Error Message:**
```
curl: (7) Failed to connect to localhost port 5000: Connection refused
```

**Not a curl issue** - this is the service not running:
1. Check service status: `sc query SCADASMSSystem`
2. Start service: `sc start SCADASMSSystem`
3. Check logs: `C:\SCADA\Logs\`

## Security Considerations

### Is it Safe to Bundle curl.exe?

**Yes, for these reasons:**

1. **Official Source**: Downloaded from official curl.se website
2. **Open Source**: curl is MIT-licensed open source software
3. **Widely Used**: curl is industry standard, included in Windows 10/11
4. **No Dependencies**: Standalone executable, no DLLs or registry entries
5. **Read-Only**: Used only for HTTP POST requests, doesn't accept input
6. **Limited Scope**: Only communicates with localhost:5000 (your service)

### Virus Scanning

Some antivirus software may flag curl.exe as suspicious because:
- It's a network tool capable of making HTTP requests
- Command-line executable
- Not signed with your company certificate

**Solutions:**
1. **Whitelist**: Add `Tools\curl.exe` to antivirus exceptions
2. **Sign**: Digitally sign curl.exe with your company certificate
3. **Educate**: Inform security team this is official curl.se binary

### Air-Gapped Security Benefits

Bundling curl.exe actually **improves security** in air-gapped environments:
- No need to connect to internet to download curl
- No need for USB drives or external downloads (attack vectors)
- Controlled, versioned, tested binary
- Same curl version across all deployments

## Licensing

### curl License (MIT/X derivative)

curl is free software distributed under an MIT-style license:

```
COPYRIGHT AND PERMISSION NOTICE

Copyright (c) 1996 - 2024, Daniel Stenberg, <daniel@haxx.se>, and many
contributors, see the THANKS file.

All rights reserved.

Permission to use, copy, modify, and distribute this software for any purpose
with or without fee is hereby granted, provided that the above copyright
notice and this permission notice appear in all copies.

[Full license text: https://curl.se/docs/copyright.html]
```

**Key Points:**
- ? Free to use commercially
- ? Free to redistribute
- ? No attribution required in UI/docs (but nice to include)
- ? No royalties or fees

### Our Usage Compliance

We comply with curl licensing by:
1. Using unmodified curl.exe binary from official source
2. Not claiming curl as our own work
3. Including documentation about curl source and license
4. Recommending users visit https://curl.se/ for updates

## Updates and Maintenance

### Updating curl.exe

To update to a newer curl version:

1. **Check Latest Version**: Visit https://curl.se/windows/
2. **Update Download Script**: Edit `download_curl.bat` with new version URL
3. **Download New Version**: Run `download_curl.bat`
4. **Test**: Verify `temp_curl\curl.exe --version`
5. **Rebuild**: Run `build.bat` to package new version
6. **Document**: Update this file and VERSION.txt

### When to Update

**Update curl.exe when:**
- Security vulnerabilities are announced
- New features are needed (e.g., HTTP/3)
- Major version releases (8.x ? 9.x)

**No need to update if:**
- Current version works fine
- No security issues
- HTTP/1.1 functionality is sufficient

### Version Tracking

Current bundled version is tracked in:
- `VERSION.txt` in deployment package
- `download_curl.bat` download URL
- This documentation file

## Alternative Solutions (Not Recommended)

If you cannot or don't want to bundle curl.exe:

### 1. Use PowerShell Instead
```powershell
# Slower and more complex than curl
Invoke-WebRequest -Uri "http://localhost:5000/api/sms/send" -Method POST -Body $json
```
**Downsides:** Slower, more complex error handling, script size

### 2. Pre-install curl on All Systems
```batch
# Must install on each SCADA PC
winget install curl.curl
```
**Downsides:** Requires internet, manual work, version inconsistencies

### 3. Use C# HTTP Client
```csharp
// Would require compiling a separate .exe
HttpClient.PostAsync(...)
```
**Downsides:** Another executable to maintain, larger size than curl

## Summary

### Benefits of Bundled curl.exe

? **Air-Gapped Compatible**: Works without internet or system curl  
? **Consistent**: Same version across all deployments  
? **Simple**: No manual installation required  
? **Small**: Only ~4 MB addition to package  
? **Licensed**: Free to redistribute under MIT license  
? **Standard**: Industry-standard tool, familiar to admins  
? **Tested**: Works identically to system curl  

### Recommendations

**For New Deployments:**
- Use `Scada_sms_bundled.bat` (auto-detects bundled vs system curl)
- Include `Tools\curl.exe` in all packages
- Document bundled curl in installation guides

**For Existing Deployments:**
- Can continue using `Scada_sms.bat` if system curl works
- Upgrade to bundled version when updating/migrating
- Keep both scripts for flexibility

**For Air-Gapped SCADA Environments:**
- **Always** use `Scada_sms_bundled.bat`
- **Always** copy `Tools\curl.exe`
- Test curl before deploying to production

## Support

### Getting Help

**curl.exe Issues:**
- Official curl docs: https://curl.se/docs/
- curl mailing list: https://lists.haxx.se/listinfo/curl-users
- curl GitHub: https://github.com/curl/curl/issues

**SCADA SMS System Issues:**
- Check `C:\SCADA\Logs\` for application logs
- Test curl manually: `curl --version`
- Verify service running: `sc query SCADASMSSystem`

### Common Questions

**Q: Is curl.exe required if Windows already has curl?**  
A: No, but bundling it ensures compatibility with older/air-gapped systems.

**Q: Can I use a different curl version?**  
A: Yes, any curl 7.x or 8.x should work. Update `download_curl.bat` for different version.

**Q: Does bundling curl.exe violate any licenses?**  
A: No, curl is MIT-licensed and free to redistribute.

**Q: Will antivirus block curl.exe?**  
A: Possibly. Whitelist the Tools folder if needed.

**Q: Can I deploy without curl.exe?**  
A: Yes, if target systems have curl installed. Use `Scada_sms.bat` instead.

---

**Last Updated**: Build v2.1 (2024)  
**curl Version**: 8.11.0  
**License**: MIT (curl), Proprietary (SCADA SMS System)
