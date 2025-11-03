using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<MockServiceStatus>();
builder.Services.AddControllers();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure pipeline
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();

// IAA AFCON SMS API Endpoint
app.MapGet("/services/SendMessage.asmx/SendMessagesReturenMessageID", async (HttpContext context, MockServiceStatus status, ILogger<Program> logger) =>
{
    try
    {
        var query = context.Request.Query;
        
        var userName = query["UserName"].FirstOrDefault();
        var password = query["Password"].FirstOrDefault();
        var senderName = query["SenderName"].FirstOrDefault();
        var sendToPhoneNumbers = query["SendToPhoneNumbers"].FirstOrDefault();
        var message = query["Message"].FirstOrDefault();
        var ccToEmail = query["CCToEmail"].FirstOrDefault() ?? "";
        var smsOperation = query["SMSOperation"].FirstOrDefault() ?? "Push";
        var deliveryDelayInMinutes = query["DeliveryDelayInMinutes"].FirstOrDefault() ?? "0";
        var expirationDelayInMinutes = query["ExpirationDelayInMinutes"].FirstOrDefault() ?? "60";
        var messageOption = query["MessageOption"].FirstOrDefault() ?? "Concatenated";
        var groupCodes = query["GroupCodes"].FirstOrDefault() ?? "";
        var price = query["Price"].FirstOrDefault() ?? "0";

        logger.LogInformation("SMS API Request - UserName: {UserName}, Phone: {Phone}, Message: {Message}", 
            userName, sendToPhoneNumbers, message);

        // Validate required parameters
        if (string.IsNullOrEmpty(userName))
            return Results.BadRequest("Missing required parameter: UserName");
        if (string.IsNullOrEmpty(password))
            return Results.BadRequest("Missing required parameter: Password");
        if (string.IsNullOrEmpty(sendToPhoneNumbers))
            return Results.BadRequest("Missing required parameter: SendToPhoneNumbers");
        if (string.IsNullOrEmpty(message))
            return Results.BadRequest("Missing required parameter: Message");

        // Authenticate
        if (userName != "d19afcsms")
            return Results.Unauthorized();

        // Simulate processing delay
        await Task.Delay(Random.Shared.Next(200, 800));

        status.ProcessMessage(message, 1, $"IAA-{DateTime.Now:yyyyMMddHHmmss}");

        // Simulate occasional failures (3% rate)
        if (Random.Shared.NextDouble() < 0.03)
        {
            status.IncrementFailures();
            var errorMessages = new[]
            {
                "SMS Provider Error: Network timeout",
                "SMS Provider Error: Service temporarily unavailable",
                "SMS Provider Error: Invalid destination number"
            };
            var errorMessage = errorMessages[Random.Shared.Next(errorMessages.Length)];
            logger.LogWarning("SMS failure for {Phone}: {Error}", sendToPhoneNumbers, errorMessage);
            return Results.BadRequest(errorMessage);
        }

        // Generate message ID
        var messageId = $"MSG{DateTime.Now:yyyyMMddHHmmssff}{Random.Shared.Next(100, 999)}";
        
        logger.LogInformation("SMS sent successfully to {Phone} - MessageID: {MessageId}", 
            sendToPhoneNumbers, messageId);

        // Set response headers
        context.Response.Headers["X-SMS-Provider"] = "IAA-AFCON-Mock";
        context.Response.Headers["X-Message-ID"] = messageId;
        
        return Results.Ok($"Message sent successfully. MessageID: {messageId}");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing SMS API request");
        return Results.Problem("Internal server error", statusCode: 500);
    }
});

// Status endpoint
app.MapGet("/status", (MockServiceStatus status) =>
{
    return Results.Ok(new
    {
        provider = "IAA AFCON Mock",
        status = "operational",
        messages_sent = status.MessagesSent,
        messages_failed = status.MessagesFailed,
        success_rate = status.MessagesSent > 0 ? 
            Math.Round((double)status.MessagesSent / (status.MessagesSent + status.MessagesFailed) * 100, 2) : 100,
        uptime = status.ServiceUptime.ToString(@"d\.hh\:mm\:ss"),
        last_message = status.LastMessageTime?.ToString("yyyy-MM-dd HH:mm:ss")
    });
});

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.Now }));

// Root endpoint
app.MapGet("/", () => Results.Json(new
{
    service = "IAA AFCON SMS Mock",
    version = "1.0",
    endpoints = new
    {
        sms = "/services/SendMessage.asmx/SendMessagesReturenMessageID",
        status = "/status",
        health = "/health"
    }
}));

Console.WriteLine("IAA AFCON SMS Mock Server Starting...");
Console.WriteLine($"Listening on: {builder.Configuration.GetValue<string>("urls") ?? "http://localhost:5555"}");
Console.WriteLine("SMS Endpoint: /services/SendMessage.asmx/SendMessagesReturenMessageID");
Console.WriteLine("Press Ctrl+C to stop");

app.Run();

public class MockServiceStatus
{
    private readonly DateTime _startTime;
    private readonly object _lock = new();

    public MockServiceStatus()
    {
        _startTime = DateTime.Now;
        MessagesSent = 0;
        MessagesFailed = 0;
    }

    public int MessagesSent { get; private set; }
    public int MessagesFailed { get; private set; }
    public TimeSpan ServiceUptime => DateTime.Now - _startTime;
    public DateTime? LastMessageTime { get; private set; }

    public void ProcessMessage(string message, int groupId, string messageId)
    {
        lock (_lock)
        {
            MessagesSent++;
            LastMessageTime = DateTime.Now;
        }
    }

    public void IncrementFailures()
    {
        lock (_lock)
        {
            MessagesFailed++;
        }
    }
}