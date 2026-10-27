# Contributing to SCADA SMS System

Thank you for your interest in contributing to the SCADA SMS System! This document provides guidelines and information for contributors.

## ?? **Project Overview**

The SCADA SMS System is a modern .NET 9 C# implementation of an industrial SMS notification system, migrated from Python to deliver superior performance, security, and Windows integration.

### **Current Status**
- ? **Core Services**: All business logic implemented and tested
- ? **Database Layer**: Entity Framework with Python schema compatibility
- ? **Dashboard UI**: Professional interface with real-time stats
- ?? **Management Pages**: Razor Pages for CRUD operations (next phase)
- ?? **Background Service**: Windows Service for SMS processing (future)

## ?? **How to Contribute**

### **Types of Contributions Welcome**
1. **?? Bug Fixes**: Fix issues and improve reliability
2. **? New Features**: Implement management pages and new functionality
3. **?? Documentation**: Improve documentation and examples
4. **?? Testing**: Add unit tests and integration tests
5. **? Performance**: Optimize performance and memory usage
6. **?? Security**: Enhance security features and practices
7. **?? UI/UX**: Improve user interface and experience

### **Priority Areas**
Current development priorities (great for new contributors):

1. **Management Pages** (High Priority)
   - Users management pages (CRUD operations)
   - Groups management pages with member assignment
   - SMS audit viewing with search/filter
   - Settings configuration pages

2. **API Controllers** (Medium Priority)
   - AJAX endpoints for dynamic operations
   - RESTful APIs for external integration
   - Real-time updates with SignalR

3. **Testing** (High Priority)
   - Unit tests for service layer
   - Integration tests for database operations
   - End-to-end tests for user workflows

## ?? **Getting Started**

### **1. Development Environment Setup**
```bash
# Prerequisites
- Visual Studio 2022 or VS Code
- .NET 9 SDK
- SQL Server (LocalDB or Express)
- Git

# Clone and setup
git clone https://github.com/[your-org]/SCADASMSSystem.git
cd SCADASMSSystem
dotnet restore
dotnet build
dotnet run
```

### **2. Understanding the Codebase**
Start with these documents:
- [`DEVELOPMENT_INSTRUCTIONS.md`](DEVELOPMENT_INSTRUCTIONS.md) - Complete setup guide
- [`PROJECT_DOCUMENTATION.md`](PROJECT_DOCUMENTATION.md) - Technical overview
- [`PROGRESS_TRACKING.md`](PROGRESS_TRACKING.md) - Component details

### **3. Architecture Overview**
```
SCADASMSSystem.Web/
??? Models/           # Entity models (User, Group, SMS, etc.)
??? Services/         # Business logic (100% complete)
??? Data/            # Entity Framework DbContext
??? Pages/           # Razor Pages (dashboard complete)
??? wwwroot/         # Static assets
??? Program.cs       # Startup configuration
```

### **4. Running the Application**
- Visit: `https://localhost:5001`
- Login with sample data (5 users, 4 groups)
- Explore dashboard and existing functionality
- Review service layer implementations

## ??? **Development Guidelines**

### **Code Standards**
- **Async/Await**: Use async patterns for all database operations
- **Error Handling**: Comprehensive try-catch with logging
- **Naming**: Follow established C# conventions (PascalCase, camelCase)
- **Documentation**: XML comments for public APIs
- **Logging**: Use ILogger for all important operations

### **Example Code Pattern**
```csharp
public class ExamplePageModel : PageModel
{
    private readonly IUserService _userService;
    private readonly ILogger<ExamplePageModel> _logger;
    
    public ExamplePageModel(IUserService userService, ILogger<ExamplePageModel> logger)
    {
        _userService = userService;
        _logger = logger;
    }
    
    public IEnumerable<User> Users { get; set; } = new List<User>();
    
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
- Use Entity Framework for all database operations
- Follow existing snake_case column mapping pattern
- Add migrations for schema changes
- Preserve compatibility with Python version

### **UI Guidelines**
- Use Bootstrap 5.3 for styling
- Follow existing dashboard patterns
- Ensure mobile responsiveness
- Include proper form validation

## ?? **Contribution Process**

### **1. Before You Start**
- Check existing issues and pull requests
- Create an issue to discuss major changes
- Review the project roadmap and priorities
- Join the discussion on existing issues

### **2. Development Workflow**
1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/amazing-feature`
3. **Develop** following the guidelines above
4. **Test** your changes thoroughly
5. **Commit** with clear messages: `git commit -m "Add amazing feature"`
6. **Push** to your branch: `git push origin feature/amazing-feature`
7. **Create** a Pull Request

### **3. Pull Request Guidelines**
- Use the [PR template](.github/pull_request_template.md)
- Include comprehensive description
- Add screenshots for UI changes
- Ensure all tests pass
- Request review from maintainers

### **4. Code Review Process**
- All submissions require review
- Reviews focus on code quality, security, and architecture
- Address feedback promptly
- Be open to suggestions and improvements

## ?? **Testing Guidelines**

### **Test Types**
1. **Unit Tests**: Test individual service methods
2. **Integration Tests**: Test database operations
3. **End-to-End Tests**: Test complete user workflows

### **Testing Framework**
```csharp
[Test]
public async Task UserService_CreateUser_ShouldReturnSuccess()
{
    // Arrange
    var user = new User { UserName = "Test User", ... };
    
    // Act
    var result = await _userService.CreateUserAsync(user);
    
    // Assert
    Assert.IsTrue(result);
}
```

### **Test Coverage Goals**
- Service layer: 80%+ coverage
- Critical paths: 100% coverage
- Error scenarios: Well covered

## ?? **Documentation Standards**

### **Code Documentation**
- XML comments for all public APIs
- Clear method and parameter descriptions
- Usage examples where helpful

```csharp
/// <summary>
/// Creates a new user in the system.
/// </summary>
/// <param name="user">The user to create.</param>
/// <returns>True if creation was successful, false otherwise.</returns>
public async Task<bool> CreateUserAsync(User user)
```

### **Project Documentation**
- Update relevant documentation files
- Include screenshots for UI changes
- Document configuration changes
- Update setup instructions if needed

## ?? **Bug Reports**

### **Before Reporting**
- Search existing issues
- Try to reproduce consistently
- Gather relevant information

### **When Reporting**
- Use the [bug report template](.github/ISSUE_TEMPLATE/bug_report.md)
- Include steps to reproduce
- Provide environment details
- Add screenshots/logs if helpful

## ? **Feature Requests**

### **When Requesting**
- Use the [feature request template](.github/ISSUE_TEMPLATE/feature_request.md)
- Explain the problem being solved
- Describe the proposed solution
- Consider implementation challenges

## ? **Questions and Support**

### **Getting Help**
- Use the [question template](.github/ISSUE_TEMPLATE/question.md)
- Check documentation first
- Search existing issues
- Be specific about what you need

### **Communication Channels**
- GitHub Issues: Primary communication
- Pull Request Reviews: Technical discussions
- Documentation: Reference material

## ??? **Labels and Project Management**

### **Issue Labels**
**Type:**
- `bug` - Something isn't working
- `enhancement` - New feature or request
- `question` - Further information requested
- `documentation` - Documentation improvements

**Priority:**
- `critical` - Blocking or security issue
- `high` - Important for next release
- `medium` - Would be nice to have
- `low` - Nice to have eventually

**Component:**
- `sms` - SMS service functionality
- `users` - User management
- `groups` - Group management
- `dashboard` - Dashboard UI
- `audit` - Audit and reporting
- `config` - Configuration
- `database` - Database related

### **Project Boards**
- **Backlog**: Planned work
- **In Progress**: Currently being worked on
- **Review**: Ready for code review
- **Testing**: Being tested
- **Done**: Completed work

## ?? **Success Metrics**

### **Code Quality**
- Build passes without warnings
- Tests pass with good coverage
- Code follows established patterns
- Documentation is complete

### **Feature Completeness**
- Requirements are met
- Error cases are handled
- Performance is acceptable
- Security is considered

## ?? **Code of Conduct**

### **Our Standards**
- Be respectful and inclusive
- Focus on constructive feedback
- Help others learn and grow
- Maintain professional communication

### **Unacceptable Behavior**
- Harassment or discrimination
- Inappropriate or offensive content
- Disruptive behavior
- Violation of privacy

## ?? **Recognition**

Contributors will be recognized through:
- GitHub contributor acknowledgments
- Release notes mentions
- Project documentation credits

## ?? **Contact**

For questions about contributing:
- Create an issue using the question template
- Tag maintainers in relevant discussions
- Use GitHub's mention system for specific help

---

**Thank you for contributing to the SCADA SMS System!** Your contributions help make industrial communication systems more reliable, secure, and efficient.

**Happy Coding!** ??