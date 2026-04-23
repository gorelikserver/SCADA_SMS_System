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

    // Typed HttpClient for SmsService — framework manages socket lifetime
    builder.Services.AddHttpClient<SmsService>();

    // Named HttpClient for SOAP workbench probe — separate from SmsService client
    builder.Services.AddHttpClient("SoapProbe", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // Add custom services
    builder.Services.AddScoped<ISmsService, SmsService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IGroupService, GroupService>();
    builder.Services.AddScoped<IHolidayService, HolidayService>();
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<IAlarmActionService, AlarmActionService>();

    // In-process mock SMS service (singleton — holds test log state)
    builder.Services.AddSingleton<IMockSmsService, MockSmsService>();

    // In-memory call log for debugging (last 50 SMS API calls, full request/response)
    builder.Services.AddSingleton<SmsCallLog>();

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

    // Apply EF Core migrations on startup (idempotent — only runs unapplied migrations)
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<SCADADbContext>();
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            startupLogger.LogInformation("=== Database Migration Starting ===");

            // Bootstrap shim: if the DB was created before EF migrations were introduced
            // (by the old DatabaseInitializationService), __EFMigrationsHistory won't exist.
            // Create it and mark the initial migration as applied so MigrateAsync only
            // runs the newer migrations that added new columns/tables.
            using var conn = context.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory'";
            var historyExists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;

            if (!historyExists)
            {
                startupLogger.LogInformation("No EF migrations history found — bootstrapping from existing schema");

                cmd.CommandText = @"
                    CREATE TABLE [__EFMigrationsHistory] (
                        [MigrationId] nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                    )";
                await cmd.ExecuteNonQueryAsync();

                // If users or scada_users exists, the initial migration was applied by the
                // old DatabaseInitializationService — mark it so EF skips re-creating tables.
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('users', 'scada_users')";
                var usersExists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;

                if (usersExists)
                {
                    cmd.CommandText = "INSERT INTO [__EFMigrationsHistory] VALUES ('20250927232936_AddGroupIdToSmsAudit', '8.0.0')";
                    await cmd.ExecuteNonQueryAsync();
                    startupLogger.LogInformation("Marked initial migration as applied (tables pre-existed)");
                }
            }

            await context.Database.MigrateAsync();
            startupLogger.LogInformation("Database migrations applied successfully");

            await SeedData.InitializeAsync(context, startupLogger);
            startupLogger.LogInformation("=== Database Migration Complete ===");
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "Critical error during database migration");

            if (!app.Environment.IsDevelopment())
                startupLogger.LogWarning("Continuing startup despite migration error (Production mode)");
            else
                throw;
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
