using Microsoft.EntityFrameworkCore;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Services;
using SCADASMSSystem.Web.Models;
using Serilog;
using Serilog.Events;
using System.IO;

// Set content root to application directory when running as Windows Service
var pathToExe = Environment.ProcessPath;
var pathToContentRoot = Path.GetDirectoryName(pathToExe)!;
Directory.SetCurrentDirectory(pathToContentRoot);

// Configure Serilog before building the application
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
var configuration = new ConfigurationBuilder()
    .SetBasePath(pathToContentRoot)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Configure Serilog with file logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .WriteTo.Console()
    .WriteTo.File(
        path: configuration["Logging:File:Path"] ?? "C:\\SCADA\\Logs\\scada-sms-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31,
        fileSizeLimitBytes: 10485760,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting SCADA SMS System from {ContentRoot}", pathToContentRoot);

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = pathToContentRoot
    });

    // Configure for Windows Service if running as service
    if (OperatingSystem.IsWindows())
    {
        builder.Host.UseWindowsService(options =>
        {
            options.ServiceName = "SCADA SMS System";
        });
    }

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add additional logging for Windows Event Log
    if (OperatingSystem.IsWindows())
    {
        builder.Logging.AddEventLog(options =>
        {
            options.SourceName = "SCADA SMS System";
            options.LogName = "Application";
        });
    }

    // Add services to the container.
    builder.Services.AddRazorPages();

    // Add API Controllers for SMS endpoints
    builder.Services.AddControllers();

    // Add Entity Framework with retry policy for production
    builder.Services.AddDbContext<SCADADbContext>(options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(30);
        });
        
        // Only enable in development
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    });

    // Add HTTP Client for SMS service
    builder.Services.AddHttpClient();

    // Add custom services
    builder.Services.AddScoped<ISmsService, SmsService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IGroupService, GroupService>();
    builder.Services.AddScoped<IHolidayService, HolidayService>();
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<IAlarmActionService, AlarmActionService>();

    // Add Background Service for SMS processing
    builder.Services.AddSingleton<SmsBackgroundService>();
    builder.Services.AddHostedService<SmsBackgroundService>(provider => provider.GetService<SmsBackgroundService>()!);

    // Configure SMS settings
    builder.Services.Configure<SmsSettings>(
        builder.Configuration.GetSection("SmsSettings"));

    // Add health checks for production monitoring
    builder.Services.AddHealthChecks()
        .AddCheck<SmsServiceHealthCheck>("sms-service");

    var app = builder.Build();

    // Ensure log directory exists
    var logPath = configuration["Logging:File:Path"] ?? "C:\\SCADA\\Logs\\scada-sms-.log";
    var logDirectory = Path.GetDirectoryName(logPath);
    if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
    {
        Directory.CreateDirectory(logDirectory);
        Log.Information("Created log directory: {LogDirectory}", logDirectory);
    }

    // Ensure database is created and seeded (important for deployment)
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<SCADADbContext>();
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            startupLogger.LogInformation("=== Database Initialization Starting ===");

            // Use intelligent table-by-table initialization
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var dbInitLogger = loggerFactory.CreateLogger<DatabaseInitializationService>();
            var dbInitService = new DatabaseInitializationService(context, dbInitLogger);
            var initResult = await dbInitService.InitializeAsync();

            if (initResult.Success)
            {
                startupLogger.LogInformation("Database initialization successful");
                
                if (initResult.CreatedTables.Any())
                {
                    startupLogger.LogInformation("  Created {Count} new table(s): {Tables}", 
                        initResult.CreatedTables.Count, 
                        string.Join(", ", initResult.CreatedTables));
                }
                else
                {
                    startupLogger.LogInformation("  All tables already exist - no changes needed");
                }

                // Seed initial data if needed
                await SeedData.InitializeAsync(context, startupLogger);
                startupLogger.LogInformation("Database seeding completed");
            }
            else
            {
                startupLogger.LogError("Database initialization failed: {Error}", initResult.ErrorMessage);
                
                if (!app.Environment.IsDevelopment())
                {
                    startupLogger.LogWarning("Continuing startup despite database initialization error (Production mode)");
                }
                else
                {
                    throw new InvalidOperationException($"Database initialization failed: {initResult.ErrorMessage}");
                }
            }
            
            startupLogger.LogInformation("=== Database Initialization Complete ===");
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "Critical error during database initialization");
            
            // In production, log and continue; in development, fail fast
            if (!app.Environment.IsDevelopment())
            {
                startupLogger.LogWarning("Continuing startup despite database initialization error (Production mode)");
            }
            else
            {
                throw;
            }
        }
    }

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    // Only use HTTPS redirection if configured
    if (builder.Configuration.GetValue<bool>("Security:RequireHttps"))
    {
        app.UseHttpsRedirection();
    }

    app.UseRouting();

    app.UseAuthorization();

    app.MapStaticAssets();
    app.MapRazorPages()
       .WithStaticAssets();

    // Map API Controllers for SMS endpoints
    app.MapControllers();

    // Map health checks for monitoring
    app.MapHealthChecks("/health");

    // Log startup information
    var appLogger = app.Services.GetRequiredService<ILogger<Program>>();
    appLogger.LogInformation("SCADA SMS System starting up...");
    appLogger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
    appLogger.LogInformation("Content Root: {ContentRoot}", pathToContentRoot);
    appLogger.LogInformation("Log directory: {LogDirectory}", logDirectory);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
