# SCADA SMS Notification System

A production-ready SMS notification system for industrial SCADA environments. Built with .NET 9 and designed for high-reliability operations in manufacturing facilities.

## Overview

This system sends SMS alerts to plant operators when equipment failures occur. It integrates directly with WinCC OA SCADA systems and manages notification groups with awareness of Jewish holidays and operator schedules.

## Key Features

- **SCADA Integration** - REST API and batch script integration with WinCC OA alarm systems
- **Group Management** - Organize operators into notification groups for targeted alerts
- **Holiday Calendar** - Automatically respects Jewish holidays based on operator preferences
- **SMS Audit Trail** - Complete delivery history for compliance and troubleshooting
- **Background Processing** - Queued SMS delivery with rate limiting and retry logic
- **Web Dashboard** - Modern interface for system administration and monitoring
- **Windows Service** - Runs as a background service with automatic startup
- **Air-Gap Compatible** - Deploy in isolated industrial networks without internet access

## Technology Stack

- **.NET 9** - Latest LTS framework
- **ASP.NET Core Razor Pages** - Web interface
- **Entity Framework Core** - Database access
- **SQL Server** - Data persistence
- **Serilog** - Structured logging
- **Bootstrap 5** - Responsive UI

## Quick Start

### Prerequisites

- Windows Server 2019+ or Windows 10/11
- .NET 9 Runtime
- SQL Server 2019+ or SQL Server Express
- SMS provider API credentials

### Installation

1. **Extract the deployment package**
   ```cmd
   unzip SCADASMSSystem_ServiceDeploy_*.zip
   ```

2. **Configure database and SMS settings**
   
   Edit `Application\appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=.\\SQLEXPRESS;Database=SCADA_SMS;Integrated Security=true"
     },
     "SmsSettings": {
       "ApiEndpoint": "https://your-sms-provider/api/send",
       "Username": "your-username",
       "Password": "your-password"
     }
   }
   ```

3. **Install as Windows Service**
   ```cmd
   cd ServiceScripts
   install_service.bat
   ```

4. **Access the web interface**
   
   Open browser to `http://localhost:5000`

The database schema is created automatically on first run with sample data for testing.

## SCADA Integration

### Alarm Action Script

Copy `Scripts\Scada_sms.bat` to your SCADA system and configure alarm actions:

```
'Run '+getalias('PCIMUTIL')+'Scada_sms.bat'+' "+'"+GetValue(...)
```

The script sends HTTP POST requests to the SMS API:

```cmd
Scada_sms.bat "Compressor failure - Zone A" 1 "85.3 PSI"
```

### API Endpoint

```http
POST /api/sms/send
Content-Type: application/json

{
  "message": "Equipment failure detected",
  "groupId": 1,
  "priority": "high"
}
```

## Configuration

### SMS Provider

The system works with any REST API-based SMS provider. Configure in `appsettings.json`:

```json
"SmsSettings": {
  "ApiEndpoint": "https://api.provider.com/send",
  "Username": "account",
  "Password": "secret",
  "RateLimit": 10,
  "RateWindow": 60
}
```

### Database

Supports SQL Server, SQL Express, or LocalDB:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=SCADA_SMS;Integrated Security=true;TrustServerCertificate=true"
}
```

### Logging

Logs are written to `C:\SCADA\Logs\` by default. Configure in `appsettings.json`:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information"
  },
  "WriteTo": [
    {
      "Name": "File",
      "Args": {
        "path": "C:\\SCADA\\Logs\\scada-sms-.log",
        "rollingInterval": "Day"
      }
    }
  ]
}
```

## Management Interface

The web dashboard provides full administrative control:

- **Dashboard** - Real-time system statistics and service health
- **Users** - Add/edit operators with phone numbers and holiday preferences
- **Groups** - Create notification groups and assign members
- **Alarms** - Link SCADA alarms to notification groups
- **Audit** - View SMS delivery history with filtering
- **Calendar** - Manage Jewish holiday dates
- **Settings** - Configure system parameters and view health checks

## Service Management

Control the Windows service using the provided scripts:

```cmd
# Check service status
check_service_status.bat

# Manage service (start/stop/restart)
manage_service.bat

# Uninstall service
uninstall_service.bat

# View diagnostics
diagnose_service.bat
```

## Building from Source

### Prerequisites

- .NET 9 SDK
- Visual Studio 2022 or VS Code

### Build Commands

```cmd
# Restore packages
dotnet restore

# Build application
dotnet build --configuration Release

# Create deployment package
build.bat
```

The build script creates a complete deployment package with:
- Self-contained application binaries
- Windows Service installation scripts
- SCADA integration scripts
- curl.exe for air-gapped systems
- Documentation

## Architecture

### Service Layer

The application uses dependency injection with clearly defined service interfaces:

- **UserService** - Operator management
- **GroupService** - Notification group management
- **SmsService** - SMS delivery and queuing
- **HolidayService** - Jewish calendar integration
- **AuditService** - SMS history tracking
- **AlarmActionService** - SCADA alarm configuration

### Background Processing

The `SmsBackgroundService` processes the SMS queue continuously:

- Deduplication (prevents duplicate messages within 5 minutes)
- Rate limiting (configurable messages per minute)
- Holiday checking (skips users who don't work on holidays)
- Retry logic (3 attempts with exponential backoff)
- Error handling (logs failures for troubleshooting)

### Database Schema

Entity Framework Code First with automatic migrations:

```
Users (operators with SMS preferences)
  ?
GroupMembers (many-to-many relationship)
  ?
Groups (notification groups)
  ?
AlarmActions (SCADA alarm configurations)

SmsAudits (delivery history)
DateDimensions (Jewish holiday calendar)
```

## Security

- CSRF protection on all forms
- XSS prevention with automatic encoding
- Parameterized SQL queries (no injection risk)
- Encrypted configuration values
- Windows integrated authentication support
- TLS/SSL for SMS API calls

## Troubleshooting

### Service Won't Start

Check Windows Event Viewer (Application log) for errors:

```cmd
eventvwr.msc
```

Common issues:
- SQL Server not running
- Database connection string incorrect
- Port 5000 already in use
- Insufficient permissions

### SMS Not Sending

1. Check service status: `check_service_status.bat`
2. Review logs: `C:\SCADA\Logs\`
3. Test SMS API credentials
4. Verify phone number format (international format required)
5. Check rate limits in configuration

### Database Connection Failed

1. Verify SQL Server is running
2. Test connection string with SSMS
3. Check Windows Firewall rules
4. Confirm database exists (auto-created on first run)

## Air-Gap Deployment

The system is designed for air-gapped industrial networks:

1. Build deployment package on internet-connected machine
2. Package includes curl.exe for HTTP requests
3. No internet access required after deployment
4. All dependencies are self-contained
5. Updates deployed manually via USB/network transfer

The `Scada_sms.bat` script automatically uses bundled curl.exe or falls back to system curl.

## Production Deployment

### Pre-Deployment Checklist

- [ ] SQL Server installed and running
- [ ] SMS provider API credentials obtained
- [ ] Phone numbers collected in international format
- [ ] Holiday preferences determined for each operator
- [ ] Network ports configured (5000 for HTTP)
- [ ] Windows Service account created (optional)
- [ ] Backup strategy defined

### Deployment Steps

1. Run build script on development machine: `build.bat`
2. Copy deployment ZIP to target server
3. Extract to `C:\SCADA\SMS\` (or preferred location)
4. Edit `appsettings.json` with production values
5. Run `install_service.bat` as Administrator
6. Verify service started: `check_service_status.bat`
7. Access web interface: `http://localhost:5000`
8. Add users and configure groups
9. Link SCADA alarms to notification groups
10. Deploy `Scada_sms.bat` to SCADA system
11. Test with sample alarm

## Monitoring

### Health Check Endpoint

```http
GET /health
```

Returns JSON with service status:

```json
{
  "status": "Healthy",
  "entries": {
    "database": {
      "status": "Healthy"
    },
    "sms-service": {
      "status": "Healthy"
    }
  }
}
```

### Performance Metrics

The dashboard shows:
- SMS queue depth
- Processing rate (messages per minute)
- Success rate (percentage)
- Last processing timestamp
- Service uptime

## License

MIT License - See LICENSE file for details

## Support

For issues and questions:
- Check logs in `C:\SCADA\Logs\`
- Review troubleshooting section above
- Open GitHub issue with logs and configuration (redact sensitive data)

---

**Built for industrial environments. Tested in production facilities.**
