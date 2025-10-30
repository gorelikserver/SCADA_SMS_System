# SCADA SMS System ???

> **A modern .NET 9 industrial SCADA SMS notification system for real-time equipment failure alerts**

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-9.0-512BD4)](https://dotnet.microsoft.com/apps/aspnet)
[![Entity Framework](https://img.shields.io/badge/Entity%20Framework-9.0-512BD4)](https://docs.microsoft.com/ef/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2019+-CC2927?logo=microsoft-sql-server)](https://www.microsoft.com/sql-server)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## ?? Overview

The **SCADA SMS System** is a production-ready notification platform designed for industrial environments. It sends targeted SMS alerts to operators during equipment failures, manages user groups for organized notifications, respects Jewish holidays for work-life balance, and provides comprehensive audit trails for regulatory compliance.

### Key Features

- ? **Real-time SMS Notifications** - Instant alerts for SCADA equipment failures
- ?? **User & Group Management** - Organize users into notification groups
- ?? **Holiday Calendar** - Jewish holiday calendar with automatic work preferences
- ?? **Audit & Compliance** - Complete SMS delivery history with timestamps
- ?? **Background Processing** - Asynchronous SMS queue with rate limiting
- ??? **Web Dashboard** - Professional management interface with real-time stats
- ?? **SCADA Integration** - RESTful API for alarm action assignments
- ?? **Windows Service** - Run as a background Windows service
- ?? **Air-gapped Support** - Deploy in isolated industrial environments
- ?? **Health Monitoring** - Built-in health checks and service monitoring

---

## ?? Quick Start

### Prerequisites

- **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **SQL Server 2019+** or **LocalDB** - [Download SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads)
- **Windows 10/11 or Windows Server 2019+** (for Windows Service)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/SCADA_SMS_System.git
   cd SCADA_SMS_System
   ```

2. **Configure database connection**
   
   Edit `appsettings.json` and update the connection string:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=.\\SQLEXPRESS;Database=SCADA_SMS;Integrated Security=true;TrustServerCertificate=true;"
     }
   }
   ```

3. **Configure SMS API settings**
   
   Update SMS provider settings in `appsettings.json`:
   ```json
   {
     "SmsSettings": {
       "ApiEndpoint": "https://your-sms-provider.com/api/send",
       "Username": "your-username",
       "Password": "your-password",
       "SenderName": "SCADA Alert"
     }
   }
   ```

4. **Build and run**
   ```bash
   dotnet restore
   dotnet build
   dotnet run
   ```

5. **Access the dashboard**
   
   Open your browser and navigate to: **http://localhost:5000**

The database will be automatically created and seeded with sample data on first run.

---

## ?? Deployment

### Option 1: Run as Web Application

```bash
# Build for release
dotnet publish -c Release -o ./publish

# Run the application
cd publish
dotnet SCADASMSSystem.Web.dll
```

### Option 2: Windows Service (Recommended for Production)

```bash
# Build for Windows Service
build_for_service.bat

# Install as Windows Service
install_service.bat

# Manage service
manage_service.bat
```

### Option 3: Air-gapped Deployment

For isolated industrial environments without internet access:

```bash
# Create deployment package
create_deployment_package.bat

# Transfer the generated package to target machine
# Extract and run install_service.bat on target machine
```

See [AIR_GAPPED_DEPLOYMENT.md](AIR_GAPPED_DEPLOYMENT.md) for detailed instructions.

---

## ??? System Architecture

### Technology Stack

```
Frontend:  ASP.NET Core Razor Pages + Bootstrap 5.3 + jQuery
Backend:   .NET 9 with Entity Framework Core 9
Database:  SQL Server 2019+ / LocalDB
Logging:   Serilog (File + Console + Windows Event Log)
Services:  Dependency Injection + Background Services
Security:  Built-in CSRF/XSS protection + encrypted passwords
```

### Project Structure

```
SCADASMSSystem.Web/
??? Controllers/          # API endpoints for SMS operations
?   ??? SmsController.cs
??? Data/                 # Entity Framework database context
?   ??? SCADADbContext.cs
??? Models/               # Entity models (User, Group, SMS, etc.)
?   ??? User.cs
?   ??? Group.cs
?   ??? GroupMember.cs
?   ??? SmsAudit.cs
?   ??? AlarmAction.cs
?   ??? DateDimension.cs
??? Services/             # Business logic services
?   ??? SmsService.cs
?   ??? UserService.cs
?   ??? GroupService.cs
?   ??? HolidayService.cs
?   ??? AuditService.cs
?   ??? AlarmActionService.cs
?   ??? SmsBackgroundService.cs
??? Pages/                # Razor Pages UI
?   ??? Index.cshtml              # Dashboard
?   ??? Users/                    # User management
?   ??? Groups/                   # Group management
?   ??? Alarms/ManageGroups.cshtml # SCADA alarm assignments
?   ??? Audit/                    # SMS audit history
?   ??? Calendar/                 # Holiday management
?   ??? Settings/                 # System configuration
?   ??? Test/                     # SMS testing
??? Scripts/              # Database scripts
??? wwwroot/              # Static files (CSS, JS, images)
??? appsettings.json      # Configuration
??? Program.cs            # Application startup
```

---

## ?? Key Features in Detail

### ?? SMS Management

- **Real-time Queue Processing** - Background service processes SMS queue continuously
- **Rate Limiting** - Configurable limits to prevent API throttling
- **Deduplication** - Prevents duplicate messages within configurable time window
- **Holiday Awareness** - Automatically skips users who don't work on holidays
- **Retry Logic** - Automatic retry with exponential backoff for failed deliveries
- **Audit Trail** - Complete history with delivery status and timestamps

### ?? User & Group Management

- **CRUD Operations** - Create, read, update, and delete users and groups
- **SMS Preferences** - Configure phone numbers and holiday work preferences
- **Group Membership** - Assign users to multiple notification groups
- **Search & Filter** - Advanced filtering by name, phone, group, holiday preference
- **Bulk Operations** - Manage multiple users/groups simultaneously

### ?? SCADA Alarm Integration

- **Alarm Action Management** - Assign notification groups to SCADA alarm actions
- **Bulk Updates** - Update multiple alarm actions at once
- **RESTful API** - `/api/sms/send` endpoint for alarm notifications
- **Air-gapped Support** - Smart curl detection for environments without curl
- **JSON Configuration** - Flexible alarm action configuration

### ?? Dashboard & Monitoring

- **Real-time Statistics** - Live view of system status and metrics
- **Service Health** - Monitor SMS service, database, and queue status
- **Performance Metrics** - Track message delivery rates and success rates
- **Configuration Management** - Adjust settings without restarting service
- **Logs & Diagnostics** - Comprehensive logging with Serilog

---

## ?? API Endpoints

### Send SMS Message

```http
POST /api/sms/send
Content-Type: application/json

{
  "groupId": 1,
  "message": "Equipment failure detected in Zone A",
  "priority": "high"
}
```

**Response:**
```json
{
  "success": true,
  "messageId": "12345",
  "recipientCount": 5,
  "deliveryStatus": "queued"
}
```

### Send Test SMS

```http
POST /api/sms/test
Content-Type: application/json

{
  "phoneNumber": "+1234567890",
  "message": "Test message from SCADA SMS System"
}
```

### Health Check

```http
GET /health
```

**Response:**
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "sms-service": "Healthy"
  },
  "timestamp": "2024-01-15T10:30:00Z"
}
```

---

## ?? Configuration

### Database Settings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=SCADA_SMS;Integrated Security=true;TrustServerCertificate=true;"
  },
  "Database": {
    "CommandTimeoutSeconds": 30,
    "EnableRetryOnFailure": true,
    "MaxRetryCount": 3
  }
}
```

### SMS Provider Settings

```json
{
  "SmsSettings": {
    "ApiEndpoint": "https://api.smsprovider.com/send",
    "Username": "your-username",
    "Password": "your-password",
    "SenderName": "SCADA Alert",
    "RateLimit": 10,
    "RateWindow": 60,
    "TimeoutSeconds": 30,
    "RetryAttempts": 3
  }
}
```

### Background Service Settings

```json
{
  "BackgroundService": {
    "ProcessingIntervalMs": 100,
    "StatisticsIntervalMs": 5000,
    "QueueWarningThreshold": 50,
    "QueueCriticalThreshold": 100
  }
}
```

### Logging Settings

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "SCADASMSSystem.Web.Services": "Verbose"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "C:\\SCADA\\Logs\\scada-sms-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 31
        }
      }
    ]
  }
}
```

---

## ?? User Guide

### Managing Users

1. Navigate to **Users** ? **Index**
2. Click **Create New User**
3. Enter user details:
   - Full name
   - Phone number (international format: +1234567890)
   - Email address
   - Holiday work preference
4. Click **Save**

### Managing Groups

1. Navigate to **Groups** ? **Index**
2. Click **Create New Group**
3. Enter group name and description
4. Click **Edit** to add members
5. Select users from the available list
6. Click **Save**

### Assigning Alarm Actions

1. Navigate to **Alarms** ? **Manage Groups**
2. Select alarm actions to configure
3. Choose notification group from dropdown
4. Click **Update Selected**
5. View confirmation of successful assignments

### Viewing Audit History

1. Navigate to **Audit** ? **Index**
2. Use filters to narrow results:
   - Date range
   - User name
   - Phone number
   - Delivery status
3. Export results to CSV if needed

### Testing SMS Delivery

1. Navigate to **Test** ? **SMS**
2. Enter phone number and message
3. Click **Send Test SMS**
4. Check audit log for delivery status

---

## ??? Development

### Build from Source

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests (if available)
dotnet test

# Run application in development mode
dotnet run --environment Development
```

### Database Migrations

The system uses automatic database initialization. The database schema is created automatically on first run.

To manually manage migrations:

```bash
# Add new migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update

# Rollback to previous migration
dotnet ef database update PreviousMigrationName
```

### Adding New Features

See [.github/copilot-instructions.md](.github/copilot-instructions.md) for development guidelines and patterns.

---

## ?? System Requirements

### Minimum Requirements

- **OS**: Windows 10 / Windows Server 2019
- **CPU**: 2 cores @ 2.0 GHz
- **RAM**: 4 GB
- **.NET**: .NET 9 Runtime
- **Database**: SQL Server 2019 Express or higher
- **Disk**: 500 MB free space (plus log storage)

### Recommended Requirements

- **OS**: Windows 11 / Windows Server 2022
- **CPU**: 4 cores @ 3.0 GHz
- **RAM**: 8 GB
- **.NET**: .NET 9 SDK (for development)
- **Database**: SQL Server 2022 Standard or higher
- **Disk**: 10 GB free space (for logs and audit history)

---

## ?? Security

- **CSRF Protection** - Built-in anti-forgery tokens for all forms
- **XSS Prevention** - Automatic HTML encoding of user input
- **SQL Injection Protection** - Parameterized queries via Entity Framework
- **Password Encryption** - Secure storage of SMS API credentials
- **TLS/SSL Support** - HTTPS support for secure communications
- **Audit Logging** - Complete audit trail of all SMS operations

---

## ?? Troubleshooting

### Database Connection Issues

```
Error: Cannot connect to SQL Server
```

**Solution:**
1. Verify SQL Server is running: `services.msc` ? SQL Server
2. Check connection string in `appsettings.json`
3. Test connection using SQL Server Management Studio
4. Ensure Windows Firewall allows SQL Server connections

### SMS Not Sending

```
Error: SMS delivery failed
```

**Solution:**
1. Check SMS API credentials in `appsettings.json`
2. Verify API endpoint is accessible
3. Check logs in `C:\SCADA\Logs\scada-sms-YYYYMMDD.log`
4. Test SMS API using **Test** ? **SMS** page
5. Verify rate limits are not exceeded

### Service Won't Start

```
Error: Windows Service failed to start
```

**Solution:**
1. Check Windows Event Viewer ? Application logs
2. Verify service account has permissions
3. Check database connectivity
4. Review logs in `C:\SCADA\Logs\`
5. Try running as console app first: `dotnet SCADASMSSystem.Web.dll`

---

## ?? License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## ?? Contributing

Contributions are welcome! Please see [.github/CONTRIBUTING.md](.github/CONTRIBUTING.md) for guidelines.

---

## ?? Support

For issues and questions:
- **GitHub Issues**: [Create an issue](https://github.com/yourusername/SCADA_SMS_System/issues)
- **Documentation**: See `.github/` folder for detailed guides
- **Email**: support@yourcompany.com

---

## ?? Acknowledgments

- Built with [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)
- UI powered by [Bootstrap 5](https://getbootstrap.com/)
- Logging by [Serilog](https://serilog.net/)
- Database management with [Entity Framework Core](https://docs.microsoft.com/ef/)

---

**Made with ?? for industrial automation professionals**
