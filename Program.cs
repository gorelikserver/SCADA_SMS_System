using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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

// Settings edited in the UI live outside the install folder, which an MSI upgrade deletes.
var settingsFile = SettingsLocation.Resolve(configuration[SettingsLocation.ConfigKey], pathToContentRoot);

// The one file sink. Logging:File:Path is what the installer's log folder sets. Serilog adds the
// date itself, so a "{Date}" left in older configs would end up literally in the file name.
var logPath = (configuration["Logging:File:Path"] ?? "C:\\SCADA\\Logs\\scada-sms-.log").Replace("{Date}", "");

// Configure Serilog with file logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .WriteTo.Console()
    .WriteTo.File(
        path: logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31,
        fileSizeLimitBytes: 10485760,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting SCADA SMS System from {ContentRoot}", pathToContentRoot);

    try
    {
        // Restrict the folder before the first secret is written into it.
        var settingsDir = Path.GetDirectoryName(settingsFile)!;
        if (!Directory.Exists(settingsDir))
        {
            Directory.CreateDirectory(settingsDir);
            SettingsLocation.RestrictFolderIfService(settingsFile);
        }
        if (SettingsLocation.EnsureSeeded(settingsFile, Path.Combine(pathToContentRoot, "appsettings.json")))
        {
            Log.Warning("Created {SettingsFile} from the SmsSettings in appsettings.json; settings edited in the UI are stored there from now on", settingsFile);
        }
    }
    catch (Exception ex)
    {
        // Alarms keep working from appsettings.json; saving settings in the UI will fail until fixed.
        Log.Error(ex, "Could not create the settings file {SettingsFile}", settingsFile);
    }

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = pathToContentRoot
    });

    // UI-edited settings override appsettings*.json; environment variables and the command line
    // still override everything.
    {
        var sources = builder.Configuration.Sources;
        var lastAppSettings = sources.Select((s, i) => (s, i))
            .Where(x => x.s is Microsoft.Extensions.Configuration.Json.JsonConfigurationSource json && (json.Path ?? "").StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.i).DefaultIfEmpty(sources.Count - 1).Max();
        sources.Insert(lastAppSettings + 1, new Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetDirectoryName(settingsFile)!),
            Path = Path.GetFileName(settingsFile),
            Optional = true,
            ReloadOnChange = true,
        });
    }

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
    const string AdminPolicy = "AdminOnly";
    builder.Services.AddRazorPages(options =>
    {
        // Viewers: dashboard, alerts, audit, calendar, help. Everything that changes who gets
        // alarms or how they are sent is admin-only.
        foreach (var folder in new[] { "/Settings", "/Users", "/Groups", "/Alarms", "/Test", "/Debug" })
            options.Conventions.AuthorizeFolder(folder, AdminPolicy);
        options.Conventions.AuthorizePage("/Account/Manage", AdminPolicy);
        options.Conventions.AllowAnonymousToPage("/Error");
    });

    // Local accounts (air-gapped sites have no identity provider). Every page needs a sign-in;
    // the SCADA API, mock provider, health check and static files stay anonymous.
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = TimeSpan.FromHours(12);
            options.SlidingExpiration = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        options.AddPolicy(AdminPolicy, policy => policy.RequireRole(AccountRoles.Admin));
    });

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
    builder.Services.AddSingleton<ISettingsStore>(sp => new SettingsStore(
        settingsFile, sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ILogger<SettingsStore>>()));
    builder.Services.AddScoped<ISettingsHistory, SettingsHistory>();
    builder.Services.AddScoped<SCADASMSSystem.Web.Services.Providers.IProviderProfileService, SCADASMSSystem.Web.Services.Providers.ProviderProfileService>();
    builder.Services.AddSingleton<IAlertNotifier, EventLogAlertNotifier>();
    builder.Services.AddScoped<IDeliveryAlertService, DeliveryAlertService>();

    // In-process mock SMS service (singleton — holds test log state)
    builder.Services.AddSingleton<IMockSmsService, MockSmsService>();

    // In-memory call log for debugging (last 50 SMS API calls, full request/response)
    builder.Services.AddSingleton<SmsCallLog>();
    builder.Services.AddSingleton<SoapXmlLog>();

    // Add Background Service for SMS processing
    builder.Services.AddSingleton<SmsBackgroundService>();
    builder.Services.AddHostedService<SmsBackgroundService>(provider => provider.GetService<SmsBackgroundService>()!);

    // Re-sends alarms Scada_sms.bat left in the spool (service was down or delivery incomplete)
    builder.Services.Configure<SpoolOptions>(builder.Configuration.GetSection("ScadaSpool"));
    builder.Services.AddHostedService<SpoolIngestService>();

    // Configure SMS settings
    builder.Services.Configure<SmsSettings>(
        builder.Configuration.GetSection("SmsSettings"));

    // Add health checks for production monitoring
    builder.Services.AddHealthChecks()
        .AddCheck<SmsServiceHealthCheck>("sms-service");

    var app = builder.Build();

    // Ensure log directory exists
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

    // Hard cutover to provider profiles: SmsService sends only with SmsSettings:Profile. A site
    // upgraded from the pre-profile version has its old REST/SOAP fields converted into a profile
    // and removed (the previous settings file stays in settings-backups). A fresh install has no
    // profile until an admin activates one in Settings > Provider; until then alarms are refused
    // and the banner says so.
    {
        var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
        try
        {
            ProviderProfile? converted = null;
            var store = app.Services.GetRequiredService<ISettingsStore>();
            if ((await store.ReadSmsSettingsAsync()).Profile == null)
            {
                var effective = app.Configuration.GetSection("SmsSettings").Get<LegacySmsSettings>();
                await store.UpdateSmsSettingsAsync(section => converted = SCADASMSSystem.Web.Services.Providers.ProfileManagement.CutoverLegacy(section, effective));
            }

            if (converted != null)
            {
                var errors = SCADASMSSystem.Web.Services.Providers.ProfileValidator.Validate(converted);
                if (errors.Count == 0)
                    startupLogger.LogWarning("Converted the old {Kind} provider settings into the active provider profile", converted.Kind);
                else
                    startupLogger.LogCritical("Converted provider profile is invalid; alarms will not be sent until it is fixed: {Errors}", string.Join("; ", errors));
            }
        }
        catch (Exception ex)
        {
            startupLogger.LogCritical(ex, "Could not convert the old provider settings into a provider profile");
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

    // Debug pages create fake audits, change settings and send test SMS: never serve them
    // outside Development, even if someone types the URL.
    if (!app.Environment.IsDevelopment())
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/Debug", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            await next();
        });
    }

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapStaticAssets().AllowAnonymous();
    app.MapRazorPages()
       .WithStaticAssets();

    // Map API Controllers for SMS endpoints
    app.MapControllers();

    // Map health checks for monitoring
    app.MapHealthChecks("/health").AllowAnonymous();

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
