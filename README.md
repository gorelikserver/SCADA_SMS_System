# SCADA SMS System ??

> **A production-ready .NET 9 industrial SCADA SMS notification system for real-time equipment failure alerts**

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-9.0-512BD4)](https://dotnet.microsoft.com/apps/aspnet)
[![Entity Framework](https://img.shields.io/badge/Entity%20Framework-9.0-512BD4)](https://docs.microsoft.com/ef/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2019+-CC2927?logo=microsoft-sql-server)](https://www.microsoft.com/sql-server)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## ?? Overview

The **SCADA SMS System** is a comprehensive, production-ready notification platform designed specifically for industrial SCADA environments. It provides intelligent SMS alerting for equipment failures, respects operator work-life balance through holiday awareness, and maintains complete audit trails for regulatory compliance.

Built with modern .NET 9 technologies, this system is actively used in industrial facilities to ensure critical equipment failures are immediately communicated to the right operators at the right time.

### ? Key Features

- ?? **Real-time SMS Notifications** - Instant alerts for SCADA equipment failures via background service
- ?? **User & Group Management** - Organize operators into notification groups with CRUD operations
- ?? **Jewish Holiday Calendar** - Automatic holiday detection with configurable work preferences
- ?? **Complete Audit Trail** - Full SMS delivery history with timestamps and status tracking
- ?? **Background Processing** - Asynchronous SMS queue with rate limiting and deduplication
- ?? **Professional Dashboard** - Modern web interface with real-time statistics and monitoring
- ?? **SCADA Integration** - RESTful API for alarm action assignments and notifications
- ?? **Windows Service Support** - Run as background service for 24/7 operation
- ?? **Air-gapped Deployment** - Deploy in isolated industrial environments without internet
- ?? **Health Monitoring** - Built-in health checks, diagnostics, and service status monitoring

---

## ?? Quick Start

### Prerequisites

- **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **SQL Server 2019+** or **SQL Server LocalDB** - [Download SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads)
- **Windows 10/11** or **Windows Server 2019+** (for Windows Service deployment)
- **SMS Provider Account** - Any REST API-based SMS provider

### 5-Minute Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/gorelikserver/SCADA_SMS_System.git
   cd SCADA_SMS_System
   ```

2. **Configure database connection**
   
   Edit `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=.\\SQLEXPRESS;Database=SCADA_SMS;Integrated Security=true;TrustServerCertificate=true;"
     }
   }
   ```

3. **Configure SMS provider**
   
   Update `appsettings.json` with your SMS provider credentials:
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
   
   Open your browser: **http://localhost:5000**

The database is automatically created and populated with sample data on first run. No manual setup required!

---

## ?? Deployment Options

### Option 1: Development Server

Perfect for testing and development:

```bash
dotnet run --environment Development
```

Access at: `http://localhost:5000`

### Option 2: Production Web Application

Deploy as a standalone web application:

```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet SCADASMSSystem.Web.dll
```

### Option 3: Windows Service (Recommended)

For production environments requiring 24/7 operation:

```bash
# Build for Windows Service
build_for_service.bat

# Install as Windows Service
install_service.bat

# Manage the service
manage_service.bat
```

The service runs in the background and starts automatically with Windows.

### Option 4: Air-gapped Industrial Environment

For isolated networks without internet access:

```bash
# On a machine with internet access
create_deployment_package.bat

# Transfer the generated ZIP package to target machine
# Extract and run:
install_service.bat
```

See [AIR_GAPPED_DEPLOYMENT.md](AIR_GAPPED_DEPLOYMENT.md) for complete instructions.

---

## ??? System Architecture

### Technology Stack

```
Frontend:   ASP.NET Core Razor Pages + Bootstrap 5.3 + Font Awesome + jQuery
Backend:    .NET 9 with Entity Framework Core 9
Database:   SQL Server 2019+ / SQL Server LocalDB
Logging:    Serilog (File + Console + Windows Event Log)
Services:   Dependency Injection + Hosted Background Services
Security:   CSRF/XSS Protection + Secure Password Storage
API:        RESTful JSON endpoints for SCADA integration
```

### Project Structure

```
SCADASMSSystem.Web/               # Main web application project
?
??? Controllers/                   # API endpoints
?   ??? SmsController.cs          # RESTful SMS API
?
??? Data/                          # Database layer
?   ??? SCADADbContext.cs         # Entity Framework context
?
??? Models/                        # Entity models
?   ??? User.cs                   # User entity with SMS preferences
?   ??? Group.cs                  # Notification group entity
?   ??? GroupMember.cs            # Many-to-many relationship
?   ??? SmsAudit.cs               # SMS delivery audit trail
?   ??? AlarmAction.cs            # SCADA alarm group assignments
?   ??? DateDimension.cs          # Jewish calendar dates
?   ??? SmsSettings.cs            # SMS provider configuration
?
??? Services/                      # Business logic layer
?   ??? IServices.cs              # Service interfaces
?   ??? SmsService.cs             # SMS sending and queue management
?   ??? UserService.cs            # User CRUD operations
?   ??? GroupService.cs           # Group and member management
?   ??? HolidayService.cs         # Jewish holiday calculations
?   ??? AuditService.cs           # SMS audit trail management
?   ??? AlarmActionService.cs     # SCADA alarm integration
?   ??? SmsBackgroundService.cs   # Background SMS processor
?   ??? DatabaseInitializationService.cs  # Auto-seeding
?   ??? SmsServiceHealthCheck.cs  # Health monitoring
?   ??? SeedData.cs               # Sample data generator
?
??? Pages/                         # Razor Pages UI
?   ??? Index.cshtml              # Dashboard with real-time stats
?   ??? Users/                    # User management CRUD
?   ?   ??? Index.cshtml          # User list with filtering
?   ?   ??? Create.cshtml         # Add new user
?   ?   ??? Edit.cshtml           # Edit user details
?   ??? Groups/                   # Group management CRUD
?   ?   ??? Index.cshtml          # Group list
?   ?   ??? Create.cshtml         # Create new group
?   ?   ??? Edit.cshtml           # Edit group + manage members
?   ??? Alarms/                   # SCADA alarm integration
?   ?   ??? ManageGroups.cshtml   # Assign groups to alarms
?   ??? Audit/                    # SMS audit history
?   ?   ??? Index.cshtml          # Advanced filtering and export
?   ??? Calendar/                 # Holiday calendar
?   ?   ??? Index.cshtml          # Jewish holiday management
?   ??? Settings/                 # System configuration
?   ?   ??? Index.cshtml          # Settings and health checks
?   ??? Test/                     # Testing utilities
?   ?   ??? Sms.cshtml            # Send test SMS
?   ??? Debug/                    # Diagnostic tools
?   ?   ??? AlarmGroupTest.cshtml # Alarm group diagnostics
?   ?   ??? GroupDiagnostics.cshtml
?   ?   ??? AuditFix.cshtml       # Audit data repair
?   ?   ??? AuditDiagnostics.cshtml
?   ?   ??? TestAudit.cshtml
?   ?   ??? ApiTest.cshtml        # API endpoint testing
?   ??? About.cshtml              # System information
?   ??? Shared/
?       ??? _Layout.cshtml        # Master layout
?
??? wwwroot/                       # Static files
?   ??? css/                      # Bootstrap + custom styles
?   ??? js/                       # JavaScript files
?   ??? lib/                      # Client libraries
?
??? Scripts/                       # Database scripts
??? appsettings.json              # Configuration
??? appsettings.Development.json  # Dev-specific config
??? appsettings.Production.json   # Production config
??? Program.cs                    # Application startup

SCADASMSService/                  # Windows Service wrapper
??? SCADASMSService.csproj

MockSmsApi/                       # Mock SMS API for testing
??? MockSmsApi.csproj

Deployment Scripts/               # Build and deployment tools
??? build.bat                     # Standard build
??? build_for_service.bat         # Build for Windows Service
??? create_deployment_package.bat # Air-gapped package creation
??? install_service.bat           # Install Windows Service
??? uninstall_service.bat         # Remove Windows Service
??? manage_service.bat            # Service management menu
??? check_service_status.bat      # Check service status
??? diagnose_service.bat          # Service diagnostics
??? check_curl.bat                # Check curl availability

Documentation/
??? README.md                     # This file
??? LICENSE                       # MIT License
??? BUILD_DEPLOYMENT_GUIDE.md     # Complete deployment guide
??? AIR_GAPPED_DEPLOYMENT.md      # Air-gapped setup instructions
??? DEPLOYMENT_QUICK_CARD.md      # Quick deployment reference
??? GIT_QUICK_REFERENCE.md        # Git commands reference
??? GIT_INITIALIZATION_SUMMARY.md # Git setup guide
```

---

## ?? Key Features in Detail

### ?? SMS Management

- **Background Queue Processing** - Dedicated background service processes SMS queue continuously
- **Smart Rate Limiting** - Configurable limits prevent API throttling (default: 10 per minute)
- **Automatic Deduplication** - Prevents duplicate messages within configurable time window
- **Holiday Awareness** - Automatically skips users who don't work on Jewish holidays
- **Retry Logic** - Exponential backoff retry for failed deliveries (configurable attempts)
- **Complete Audit Trail** - Every SMS logged with timestamp, status, and delivery details
- **API Integration** - RESTful endpoints for external SCADA system integration

### ?? User & Group Management

- **Full CRUD Operations** - Create, read, update, and delete users and groups
- **SMS Preferences** - Configure phone numbers in international format
- **Holiday Configuration** - Per-user holiday work preferences
- **Multi-group Membership** - Users can belong to multiple notification groups
- **Advanced Search** - Filter by name, phone, group membership, holiday preference
- **Bulk Operations** - Update multiple users or groups simultaneously
- **Member Management** - Intuitive interface for adding/removing group members

### ?? SCADA Alarm Integration

- **Alarm Action Management** - Assign notification groups to specific SCADA alarms
- **Bulk Assignment** - Update multiple alarm actions in one operation
- **RESTful API** - `/api/sms/send` endpoint for triggering notifications
- **JSON Configuration** - Flexible alarm action configuration via JSON files
- **Air-gapped Support** - Smart curl detection and fallback mechanisms
- **Group Diagnostics** - Built-in tools to verify alarm-to-group assignments

### ?? Dashboard & Monitoring

- **Real-time Statistics** - Live metrics updated every 5 seconds
- **Service Health Monitoring** - Monitor SMS service, database, and queue status
- **Performance Metrics** - Track delivery rates, success rates, and queue depth
- **Configuration Management** - Update settings via web interface
- **Comprehensive Logging** - Serilog with file, console, and Windows Event Log sinks
- **Health Check Endpoints** - `/health` endpoint for external monitoring systems

---

## ?? API Reference

### Send SMS to Group

Trigger SMS notifications for all members of a group:

```http
POST /api/sms/send
Content-Type: application/json

{
  "groupId": 1,
  "message": "CRITICAL: Equipment failure detected in Zone A - Pressure dropping rapidly",
  "priority": "high"
}
```

**Response:**
```json
{
  "success": true,
  "messageId": "abc123",
  "recipientCount": 5,
  "deliveryStatus": "queued",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Send Test SMS

Send a test message to verify SMS provider configuration:

```http
POST /api/sms/test
Content-Type: application/json

{
  "phoneNumber": "+1234567890",
  "message": "Test message from SCADA SMS System"
}
```

**Response:**
```json
{
  "success": true,
  "messageId": "test123",
  "deliveryStatus": "sent",
  "timestamp": "2024-01-15T10:31:00Z"
}
```

### Check Service Status

Monitor system health:

```http
GET /api/sms/status
```

**Response:**
```json
{
  "status": "Healthy",
  "queueDepth": 3,
  "processingRate": 9.5,
  "successRate": 98.7,
  "lastProcessed": "2024-01-15T10:32:15Z"
}
```

### Health Check Endpoint

For external monitoring systems (e.g., Nagios, Prometheus):

```http
GET /health
```

**Response:**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "database": {
      "status": "Healthy",
      "description": "Database is accessible",
      "duration": "00:00:00.0521234"
    },
    "sms-service": {
      "status": "Healthy",
      "description": "SMS service is operational",
      "duration": "00:00:00.0123456"
    }
  }
}
```

---

## ?? Configuration Guide

### Database Settings

Configure SQL Server connection in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=SCADA_SMS;Integrated Security=true;TrustServerCertificate=true;"
  },
  "Database": {
    "CommandTimeoutSeconds": 30,
    "EnableRetryOnFailure": true,
    "MaxRetryCount": 3,
    "MaxRetryDelaySeconds": 30
  }
}
```

**Connection String Examples:**

```
# SQL Server Express (Local)
Server=.\\SQLEXPRESS;Database=SCADA_SMS;Integrated Security=true;TrustServerCertificate=true;

# SQL Server (Network)
Server=192.168.1.100;Database=SCADA_SMS;User Id=scada_user;Password=your_password;TrustServerCertificate=true;

# SQL Server LocalDB
Server=(localdb)\\mssqllocaldb;Database=SCADA_SMS;Integrated Security=true;
```

### SMS Provider Settings

Configure your SMS provider in `appsettings.json`:

```json
{
  "SmsSettings": {
    "ApiEndpoint": "https://api.smsprovider.com/send",
    "Username": "your-username",
    "Password": "your-password",
    "SenderName": "SCADA Alert",
    "TimeoutSeconds": 30,
    "RetryAttempts": 3,
    "RetryDelaySeconds": 5,
    "RateLimit": 10,
    "RateWindow": 60
  }
}
```

**Supported SMS Providers:**
- Any REST API-based SMS provider
- Tested with IAA/Afcon format
- Configurable request/response formats

### Background Service Settings

Configure SMS queue processing:

```json
{
  "BackgroundService": {
    "ProcessingIntervalMs": 100,
    "StatisticsIntervalMs": 5000,
    "QueueWarningThreshold": 50,
    "QueueCriticalThreshold": 100,
    "EnableAutoRetry": true
  }
}
```

### Logging Configuration

Configure Serilog logging:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "SCADASMSSystem.Web.Services": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "C:\\SCADA\\Logs\\scada-sms-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 31,
          "fileSizeLimitBytes": 10485760,
          "rollOnFileSizeLimit": true,
          "shared": true
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

---

## ?? User Guide

### Managing Users

1. **Navigate to Users ? Index**
2. **Create a new user:**
   - Click **Create New User**
   - Enter full name (e.g., "John Smith")
   - Enter phone number in international format (e.g., "+1234567890")
   - Enter email address
   - Set holiday work preference (works on holidays: Yes/No)
   - Click **Save**

3. **Edit existing user:**
   - Click **Edit** next to user name
   - Update details as needed
   - Click **Save**

4. **Search and filter:**
   - Use search box to filter by name or phone
   - Filter by holiday preference
   - Results update automatically

### Managing Groups

1. **Navigate to Groups ? Index**
2. **Create a new group:**
   - Click **Create New Group**
   - Enter group name (e.g., "Night Shift Operators")
   - Enter description (e.g., "All operators working night shift")
   - Click **Save**

3. **Add members to group:**
   - Click **Edit** next to group name
   - Select users from available list
   - Click **Add to Group**
   - Review current members
   - Click **Save**

4. **Remove members:**
   - In group edit page, click **Remove** next to member name
   - Member is removed from group but user record remains

### Assigning SCADA Alarms to Groups

1. **Navigate to Alarms ? Manage Groups**
2. **Assign groups to alarms:**
   - View list of all alarm actions
   - Select alarm actions using checkboxes
   - Choose notification group from dropdown
   - Click **Update Selected**
   - Confirmation message appears

3. **Bulk operations:**
   - Select **Select All** to assign all alarms to one group
   - Select individual alarms for targeted assignment
   - Clear assignments by selecting "-- Select Group --"

4. **Verify assignments:**
   - Use **Debug ? Group Diagnostics** to verify alarm assignments
   - Check that alarms are correctly mapped to groups

### Viewing SMS Audit History

1. **Navigate to Audit ? Index**
2. **Filter audit records:**
   - Select date range (From/To dates)
   - Filter by user name
   - Filter by phone number
   - Filter by delivery status (All/Sent/Failed/Pending)
   - Click **Filter**

3. **View details:**
   - Audit table shows:
     - Timestamp of SMS
     - Recipient name and phone
     - Message content
     - Delivery status
     - Error details (if failed)

4. **Export data:**
   - Click **Export to CSV** for offline analysis
   - Use for compliance reporting

### Managing Holiday Calendar

1. **Navigate to Calendar ? Index**
2. **View holidays:**
   - Calendar shows all Jewish holidays
   - Holidays are pre-populated for multiple years
   - Includes major holidays: Rosh Hashanah, Yom Kippur, Passover, etc.

3. **Add custom holiday:**
   - Click **Add Holiday**
   - Enter date and holiday name
   - Click **Save**

4. **Holiday behavior:**
   - Users marked as "does not work on holidays" won't receive SMS on holidays
   - Background service automatically checks holiday status
   - Manual override available via API if needed

### Testing SMS Delivery

1. **Navigate to Test ? SMS**
2. **Send test message:**
   - Enter phone number (international format)
   - Enter test message
   - Click **Send Test SMS**
   - View delivery result on page

3. **Check audit log:**
   - Navigate to Audit ? Index
   - Verify test message appears in log
   - Check delivery status

### System Settings & Health

1. **Navigate to Settings ? Index**
2. **View system status:**
   - SMS service status (Running/Stopped)
   - Database connection status
   - Queue depth and processing rate
   - Success rate statistics

3. **Update configuration:**
   - Modify SMS provider settings
   - Update rate limits
   - Change logging levels
   - Click **Save Settings**

4. **Health checks:**
   - View real-time health status
   - Check service uptime
   - Review error logs

---

## ??? Development Guide

### Build from Source

```bash
# Clone repository
git clone https://github.com/gorelikserver/SCADA_SMS_System.git
cd SCADA_SMS_System

# Restore NuGet packages
dotnet restore

# Build solution
dotnet build

# Run in development mode
dotnet run --environment Development
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test SCADASMSSystem.Tests

# Run with code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Database Migrations

The system uses automatic database initialization. Schema is created on first run.

**Manual migration management:**

```bash
# Add new migration
dotnet ef migrations add MigrationName

# Apply migrations to database
dotnet ef database update

# Rollback to specific migration
dotnet ef database update PreviousMigrationName

# Generate SQL script for deployment
dotnet ef migrations script
```

### Code Structure & Patterns

This project follows clean architecture principles:

- **Razor Pages Pattern** - Not MVC or Blazor
- **Dependency Injection** - All services registered in `Program.cs`
- **Repository Pattern** - Services abstract database access
- **Async/Await** - All I/O operations are asynchronous
- **Entity Framework Core** - Code-first database approach
- **Background Services** - Hosted services for continuous processing

**Service Registration Example:**

```csharp
// In Program.cs
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddHostedService<SmsBackgroundService>();
```

**Service Usage Pattern:**

```csharp
public class UsersModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersModel> _logger;
    
    public UsersModel(IUserService userService, ILogger<UsersModel> logger)
    {
        _userService = userService;
        _logger = logger;
    }
    
    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            Users = await _userService.GetAllUsersAsync();
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading users");
            return RedirectToPage("/Error");
        }
    }
}
```

### Adding New Features

See [.github/copilot-instructions.md](.github/copilot-instructions.md) for detailed development guidelines.

**Key principles:**
- Follow existing patterns in `Pages/` directory
- Use dependency injection for all services
- Implement proper error handling and logging
- Write async code for all I/O operations
- Follow Bootstrap 5.3 UI conventions

---

## ?? System Requirements

### Minimum Requirements

| Component | Requirement |
|-----------|-------------|
| **OS** | Windows 10 (build 1809+) or Windows Server 2019+ |
| **CPU** | 2 cores @ 2.0 GHz |
| **RAM** | 4 GB |
| **.NET** | .NET 9 Runtime |
| **Database** | SQL Server 2019 Express or higher |
| **Disk** | 500 MB free (plus log storage) |
| **Network** | HTTP/HTTPS access to SMS provider API |

### Recommended Requirements (Production)

| Component | Requirement |
|-----------|-------------|
| **OS** | Windows 11 or Windows Server 2022 |
| **CPU** | 4 cores @ 3.0 GHz |
| **RAM** | 8 GB |
| **.NET** | .NET 9 SDK (for development) |
| **Database** | SQL Server 2022 Standard or higher |
| **Disk** | 10 GB free (for logs and audit history) |
| **Network** | Dedicated network connection |

---

## ?? Security Features

- ? **CSRF Protection** - Anti-forgery tokens on all forms
- ? **XSS Prevention** - Automatic HTML encoding of user input
- ? **SQL Injection Protection** - Parameterized queries via Entity Framework
- ? **Secure Password Storage** - Encrypted SMS API credentials
- ? **TLS/SSL Support** - HTTPS support for secure communications
- ? **Complete Audit Logging** - Every SMS operation logged
- ? **Input Validation** - Server-side validation on all inputs
- ? **Rate Limiting** - Prevents API abuse
- ? **Error Handling** - Secure error pages without information leakage

---

## ?? Troubleshooting

### Database Connection Issues

**Symptom:** `Cannot connect to SQL Server`

**Solutions:**
1. Verify SQL Server service is running:
   ```
   services.msc ? SQL Server (SQLEXPRESS)
   ```
2. Check connection string in `appsettings.json`
3. Test connection with SQL Server Management Studio (SSMS)
4. Check Windows Firewall rules:
   ```powershell
   netsh advfirewall firewall show rule name=all | findstr SQL
   ```
5. Enable TCP/IP protocol in SQL Server Configuration Manager

### SMS Not Sending

**Symptom:** `SMS delivery failed` or messages stuck in queue

**Solutions:**
1. Check SMS API credentials in `appsettings.json`
2. Verify API endpoint is accessible:
   ```powershell
   Test-NetConnection -ComputerName api.smsprovider.com -Port 443
   ```
3. Review logs: `C:\SCADA\Logs\scada-sms-YYYYMMDD.log`
4. Test SMS API using **Test ? SMS** page
5. Check rate limits in `SmsSettings`
6. Verify phone numbers in international format

### Windows Service Issues

**Symptom:** `Service failed to start` or service crashes

**Solutions:**
1. Check Windows Event Viewer:
   ```
   eventvwr.msc ? Windows Logs ? Application
   ```
2. Verify service account permissions
3. Check database connectivity from service account
4. Review service logs in `C:\SCADA\Logs\`
5. Try running as console app first:
   ```
   cd C:\SCADA\SMS
   dotnet SCADASMSSystem.Web.dll
   ```
6. Run diagnostics:
   ```
   diagnose_service.bat
   ```

### Queue Buildup

**Symptom:** Queue depth increasing, messages not processing

**Solutions:**
1. Check SMS provider API status
2. Review rate limit settings
3. Check for network connectivity issues
4. Increase `ProcessingIntervalMs` if too aggressive
5. Review failed message logs
6. Temporarily pause queue and clear failed messages

### Performance Issues

**Symptom:** Slow page loads or high CPU usage

**Solutions:**
1. Check database query performance
2. Review log file size and retention
3. Optimize Entity Framework queries
4. Increase server resources
5. Enable database query caching
6. Review `appsettings.json` performance settings

---

## ?? License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

**MIT License Summary:**
- ? Commercial use allowed
- ? Modification allowed
- ? Distribution allowed
- ? Private use allowed
- ?? No warranty provided
- ?? No liability accepted

---

## ?? Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork the repository**
2. **Create a feature branch** (`git checkout -b feature/AmazingFeature`)
3. **Commit your changes** (`git commit -m 'Add some AmazingFeature'`)
4. **Push to branch** (`git push origin feature/AmazingFeature`)
5. **Open a Pull Request**

See [.github/CONTRIBUTING.md](.github/CONTRIBUTING.md) for detailed guidelines.

---

## ?? Support & Contact

### Getting Help

- **?? Documentation** - Check `.github/` folder and markdown files
- **?? Bug Reports** - [Create an issue](https://github.com/gorelikserver/SCADA_SMS_System/issues/new?template=bug_report.md)
- **?? Feature Requests** - [Request a feature](https://github.com/gorelikserver/SCADA_SMS_System/issues/new?template=feature_request.md)
- **? Questions** - [Ask a question](https://github.com/gorelikserver/SCADA_SMS_System/issues/new?template=question.md)
- **?? Developer** - [GitHub Profile](https://github.com/gorelikserver)

### Useful Resources

- [.NET 9 Documentation](https://docs.microsoft.com/dotnet/core/whats-new/dotnet-9)
- [ASP.NET Core Razor Pages](https://docs.microsoft.com/aspnet/core/razor-pages/)
- [Entity Framework Core](https://docs.microsoft.com/ef/core/)
- [SQL Server Documentation](https://docs.microsoft.com/sql/)

---

## ?? Acknowledgments

- Built with ?? for the industrial automation community
- Powered by .NET 9 and ASP.NET Core
- UI components from Bootstrap 5.3 and Font Awesome
- Logging by Serilog
- Database access via Entity Framework Core

---

## ?? Project Status

| Component | Status | Coverage |
|-----------|--------|----------|
| **Core Services** | ? Complete | 100% |
| **Database Layer** | ? Complete | 100% |
| **Web Dashboard** | ? Complete | 100% |
| **Management Pages** | ? Complete | 100% |
| **API Endpoints** | ? Complete | 100% |
| **Background Service** | ? Complete | 100% |
| **Windows Service** | ? Complete | 100% |
| **Deployment Tools** | ? Complete | 100% |
| **Documentation** | ? Complete | 100% |
| **Unit Tests** | ?? In Progress | 60% |

**?? This is a production-ready system actively used in industrial facilities.**

---

Made with ?? by [gorelikserver](https://github.com/gorelikserver)
