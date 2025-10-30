# GitHub Copilot Instructions - SCADA SMS System

## ?? **AI Assistant Context**

This file provides essential context for GitHub Copilot and other AI assistants working on the SCADA SMS System .NET 9 project.

---

## ?? **Project Overview**

### **What We're Building**
A modern .NET 9 C# SCADA SMS Notification System that:
- Sends SMS alerts to industrial operators during equipment failures
- Manages user groups for targeted notifications  
- Respects Jewish holidays for work-life balance
- Provides complete audit trails for regulatory compliance
- Offers a professional web dashboard for system management

### **Current Status**
- ? **Core Services**: All business logic implemented and tested
- ? **Database Layer**: Entity Framework with full schema implementation
- ? **Dashboard UI**: Professional interface with real-time stats
- ? **Management Pages**: Complete Razor Pages CRUD interface
- ? **Background Service**: SMS processing and queue management
- ? **Production Ready**: Deployment packages and monitoring

---

## ??? **Architecture & Technology Stack**

### **Technology Stack**
```
Frontend:  ASP.NET Core Razor Pages + Bootstrap 5.3 + jQuery
Backend:   .NET 9 with Entity Framework Core 9
Database:  SQL Server / LocalDB
Services:  Dependency Injection + Background Services
Security:  Built-in CSRF/XSS protection + encrypted passwords
```

### **Project Structure**
```
SCADASMSSystem.Web/
??? Data/SCADADbContext.cs          # EF Core database context
??? Models/                         # Entity models (User, Group, SMS, etc.)
??? Services/                       # Business logic services (100% complete)
??? Pages/                          # Razor Pages (complete interface)
??? Controllers/                    # API controllers for SMS operations
??? wwwroot/                        # Static assets (CSS, JS, images)
??? appsettings.json               # Configuration
??? Program.cs                     # Startup configuration
```

---

## ?? **AI Assistant Guidelines**

### **?? IMPORTANT: NO DOCUMENTATION FILES**
**DO NOT create markdown documentation files** unless explicitly requested by the user. This includes:
- ? No `*_FIX.md`, `*_COMPLETE.md`, `*_IMPROVEMENTS.md` files
- ? No `*_SUMMARY.md`, `*_IMPLEMENTATION.md`, `*_GUIDE.md` files
- ? No `GIT_*.md`, `CURL_*.md`, `BUILD_*.md` files
- ? No `DEPLOYMENT_*.md`, `AIR_GAPPED_*.md` files
- ? Only modify code files and essential project files

**Exception**: You may create/update these documentation files ONLY:
- ? `README.md` (project readme)
- ? `.github/` folder files (GitHub templates and workflows)
- ? Essential technical documentation explicitly requested

### **When Working on This Project, Always:**

1. **Use Razor Pages Pattern**: This is a Razor Pages project, not MVC or Blazor
2. **Follow Existing Patterns**: Reference implemented pages for consistency
3. **Use Dependency Injection**: All services are registered and should be injected
4. **Implement Async/Await**: All database operations must be asynchronous
5. **Include Error Handling**: Comprehensive try-catch with logging
6. **Follow Bootstrap 5.3**: Use existing CSS framework and patterns
7. **Make Code Changes Only**: Focus on implementing features in code, not documentation

### **Service Layer Guidelines**
```csharp
// Always inject services via constructor
public class ExamplePageModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ILogger<ExamplePageModel> _logger;
    
    public ExamplePageModel(IUserService userService, ILogger<ExamplePageModel> logger)
    {
        _userService = userService;
        _logger = logger;
    }
    
    // Always use async patterns
    public async Task OnGetAsync()
    {
        try
        {
            Users = await _userService.GetAllUsersAsync();
            _logger.LogInformation("Loaded {Count} users", Users.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading users");
            // Handle error appropriately
        }
    }
}
```

### **Database Guidelines**
- **Schema Design**: Uses snake_case column naming (`user_id`, `phone_number`, etc.)
- **Entity Framework**: All database operations through EF Core 9
- **Auto-Creation**: Database auto-created with sample data on first run
- **Relationships**: Proper many-to-many relationships configured

---

## ? **Current Implementation Status**

### **? Completed Components (100%)**
- **Entity Models**: User, Group, GroupMember, SmsAudit, DateDimension, SmsSettings, AlarmAction
- **Business Services**: UserService, GroupService, SmsService, HolidayService, AuditService, AlarmActionService
- **Database Context**: Full EF Core setup with relationships
- **Dashboard**: Professional UI with real-time statistics
- **Management Pages**: Complete CRUD interface for all entities
- **Background Service**: SMS queue processing and delivery
- **API Controllers**: RESTful endpoints for external integration
- **Configuration**: Dependency injection and settings management
- **Sample Data**: Automatic seeding with realistic test data
- **SCADA Integration**: Alarm group assignment and management
- **Deployment**: Windows Service, air-gapped support, build automation

### **??? Available Management Interface**
```
? Pages/Index.cshtml           # Dashboard with real-time stats
? Pages/Users/Index.cshtml     # List users with search/filter
? Pages/Users/Create.cshtml    # Add new user form
? Pages/Users/Edit.cshtml      # Edit user details
? Pages/Groups/Index.cshtml    # List groups with member counts
? Pages/Groups/Create.cshtml   # Create new group
? Pages/Groups/Edit.cshtml     # Edit group + manage members
? Pages/Alarms/ManageGroups.cshtml  # SCADA alarm group assignments
? Pages/Audit/Index.cshtml     # SMS history with advanced filtering
? Pages/Calendar/Index.cshtml  # Holiday calendar management
? Pages/Settings/Index.cshtml  # System configuration and health
? Pages/Test/Sms.cshtml        # SMS testing interface
? Pages/About.cshtml           # System information
```

**?? System Status: 100% Complete Production-Ready System**

---

## ?? **Key System Features**

### **SMS Management**
- Real-time SMS queue processing
- Rate limiting and deduplication
- Holiday-aware delivery (Jewish calendar support)
- Complete audit trail with delivery status
- API endpoints for external integration

### **User & Group Management**
- User CRUD with SMS preferences
- Holiday work preferences
- Group management with member assignments
- Advanced filtering and search capabilities

### **SCADA Alarm Integration**
- Alarm action management with group assignments
- Bulk update capabilities
- Smart curl detection for air-gapped systems
- RESTful API for alarm notifications

### **Monitoring & Administration**
- Real-time dashboard with system health
- SMS service status monitoring
- Configuration management interface
- Health check endpoints
- Comprehensive logging

### **Industrial SCADA Integration**
- RESTful API for alarm integration
- JSON-based configuration
- Windows Service capability
- Enterprise security features
- Audit compliance ready

---

## ?? **Getting Started for AI Assistants**

### **To Add New Features:**
1. **Implement in Code**: Focus on .cs, .cshtml, .csproj files
2. **Follow Patterns**: Use existing Razor Pages patterns in `Pages/` directory
3. **Use DI**: Inject services via constructor
4. **Error Handling**: Implement proper try-catch and logging
5. **Follow UI Conventions**: Use Bootstrap 5.3 styling
6. **No Documentation**: Don't create markdown files unless explicitly requested

### **To Modify Existing Features:**
1. Check existing implementation patterns
2. Maintain backward compatibility
3. Test thoroughly before committing
4. Update inline code comments only

### **To Debug Issues:**
1. Check application logs
2. Verify database connectivity
3. Test API endpoints
4. Review configuration settings

### **To Respond to User Requests:**
1. **Ask clarifying questions** if requirements are unclear
2. **Make code changes directly** - don't create documentation
3. **Explain changes briefly** in commit messages or inline comments
4. **Test changes** mentally before suggesting

---

## ?? **Code Comments & Documentation**

### **Inline Comments (? Encouraged)**
```csharp
// Use inline comments to explain complex logic
public async Task<bool> ProcessSmsAsync(SmsRequest request)
{
    // Check if holiday and user doesn't work on holidays
    if (await _holidayService.IsHolidayAsync(DateTime.Today) && !user.WorksOnHolidays)
    {
        _logger.LogInformation("Skipping SMS for user {UserId} due to holiday", user.UserId);
        return false;
    }
    
    // Rate limiting check
    // ...existing code...
}
```

### **XML Documentation (? Encouraged for public APIs)**
```csharp
/// <summary>
/// Sends an SMS message to all members of the specified group.
/// </summary>
/// <param name="groupId">The ID of the group to notify</param>
/// <param name="message">The SMS message content</param>
/// <returns>True if SMS was sent successfully, false otherwise</returns>
public async Task<bool> SendGroupSmsAsync(int groupId, string message)
{
    // Implementation
}
```

### **Separate Documentation Files (? Avoid)**
- Don't create separate markdown files for every change
- Use commit messages for change documentation
- Use inline comments for complex logic explanation
- Update README.md only when adding major features

---

## ?? **Best Practices Summary**

### **DO:**
? Focus on code implementation  
? Use existing patterns and conventions  
? Add inline comments for complex logic  
? Use XML documentation for public APIs  
? Test changes thoroughly  
? Follow C# and .NET best practices  
? Use async/await consistently  
? Implement proper error handling  

### **DON'T:**
? Create markdown documentation files  
? Over-document simple changes  
? Deviate from existing patterns  
? Skip error handling  
? Ignore logging  
? Use blocking I/O operations  
? Hard-code configuration values  
? Create files without user request  

---

## ?? **Quick Reference**

### **Common Services**
- `IUserService` - User management operations
- `IGroupService` - Group and member management
- `ISmsService` - SMS sending and processing
- `IHolidayService` - Jewish holiday calendar
- `IAuditService` - SMS audit trail
- `IAlarmActionService` - SCADA alarm integration

### **Common Models**
- `User` - User entity with SMS preferences
- `Group` - Notification group entity
- `GroupMember` - Many-to-many relationship
- `SmsAudit` - SMS delivery audit trail
- `AlarmAction` - SCADA alarm group assignments
- `DateDimension` - Jewish calendar dates

### **API Endpoints**
- `POST /api/sms/send` - Send SMS message
- `POST /api/sms/test` - Send test SMS
- `GET /api/sms/status` - Service status
- `GET /health` - Health check endpoint

---

**System is production-ready and fully functional for industrial SCADA SMS notifications.**

**Remember: Focus on code, not documentation. Make changes, don't document changes.**