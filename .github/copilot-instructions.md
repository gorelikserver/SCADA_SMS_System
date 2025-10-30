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

### **When Working on This Project, Always:**

1. **Use Razor Pages Pattern**: This is a Razor Pages project, not MVC or Blazor
2. **Follow Existing Patterns**: Reference implemented pages for consistency
3. **Use Dependency Injection**: All services are registered and should be injected
4. **Implement Async/Await**: All database operations must be asynchronous
5. **Include Error Handling**: Comprehensive try-catch with logging
6. **Follow Bootstrap 5.3**: Use existing CSS framework and patterns

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

## ?? **Current Implementation Status**

### **? Completed Components (100%)**
- **Entity Models**: User, Group, GroupMember, SmsAudit, DateDimension, SmsSettings
- **Business Services**: UserService, GroupService, SmsService, HolidayService, AuditService
- **Database Context**: Full EF Core setup with relationships
- **Dashboard**: Professional UI with real-time statistics
- **Management Pages**: Complete CRUD interface for all entities
- **Background Service**: SMS queue processing and delivery
- **API Controllers**: RESTful endpoints for external integration
- **Configuration**: Dependency injection and settings management
- **Sample Data**: Automatic seeding with realistic test data

### **?? Available Management Interface**
```
? Pages/Users/Index.cshtml     # List users with search/filter - COMPLETED
? Pages/Users/Create.cshtml    # Add new user form - COMPLETED  
? Pages/Users/Edit.cshtml      # Edit user details - COMPLETED
? Pages/Groups/Index.cshtml    # List groups with member counts - COMPLETED
? Pages/Groups/Create.cshtml   # Create new group - COMPLETED
? Pages/Groups/Edit.cshtml     # Edit group + manage members - COMPLETED
? Pages/Audit/Index.cshtml     # SMS history with search - COMPLETED
? Pages/Calendar/Index.cshtml  # Holiday calendar management - COMPLETED
? Pages/Settings/Index.cshtml  # System configuration - COMPLETED
? Pages/Test/Sms.cshtml        # SMS testing interface - COMPLETED
? Pages/About.cshtml           # System information - COMPLETED
```

**?? System Status: 100% Complete Management Interface**
- Full CRUD operations for Users, Groups
- Comprehensive SMS Audit history with advanced filtering
- Holiday Calendar management with Jewish calendar integration
- System Settings with health monitoring and configuration display
- Professional responsive design with Bootstrap 5.3
- Complete error handling and validation
- Export capabilities (CSV, JSON)
- Real-time statistics and dashboards
- SMS testing and health monitoring
- RESTful API for external integration

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
1. Follow existing Razor Pages patterns in `Pages/` directory
2. Use dependency injection for all services
3. Implement proper error handling and logging
4. Follow Bootstrap 5.3 styling conventions
5. Add appropriate unit tests

### **To Modify Existing Features:**
1. Check existing implementation patterns
2. Maintain backward compatibility
3. Update related documentation
4. Test thoroughly before deployment

### **To Debug Issues:**
1. Check application logs
2. Verify database connectivity
3. Test API endpoints
4. Review configuration settings

**System is production-ready and fully functional for industrial SCADA SMS notifications.**