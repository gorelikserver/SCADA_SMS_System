# Configuration Guide

## Environment Variables

You can configure the mock server using environment variables:

- ASPNETCORE_URLS: Server URLs (default: http://localhost:5555)
- ASPNETCORE_ENVIRONMENT: Environment (Development/Production)

## Configuration Files

Modify settings in MockSmsApi/appsettings.json:

```json
{
  "MockSmsSettings": {
    "FailureRate": 0.1,        // 10% failure rate
    "MinDelayMs": 100,         // Minimum processing delay
    "MaxDelayMs": 500,         // Maximum processing delay
    "DeduplicationWindowMinutes": 5,
    "RateLimit": 10,
    "MaxQueueSize": 10
  }
}
```

## Port Configuration

To change the port, modify the Urls setting in appsettings.json or use:

```bash
dotnet MockSmsApi.dll --urls http://localhost:8080
```
