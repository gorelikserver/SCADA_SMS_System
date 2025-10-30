# Mock SMS API Server - Deployment Package

## ?? Quick Start

### Windows (Command Prompt)
```cmd
start_server.bat
```

### Windows (PowerShell)
```powershell
.\start_server.ps1
```

### Cross-Platform
```bash
cd MockSmsApi
dotnet MockSmsApi.dll
```

## ?? Access Points

- **API Server**: http://localhost:5555
- **Swagger UI**: http://localhost:5555/swagger
- **Health Check**: http://localhost:5555/api/sms/health
- **Legacy Status**: http://localhost:5555/status

## ?? Available Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| POST /api/sms/send | POST | Send SMS message |
| POST /api/sms/test | POST | Send test SMS |
| GET /api/sms/status | GET | Service status |
| GET /api/sms/health | GET | Health check |
| POST / | POST | Legacy SMS endpoint |
| GET /status | GET | Legacy status |

## ?? Requirements

- .NET 9 Runtime (included with deployment)
- Port 5555 available
- Windows, Linux, or macOS

## ?? Features

- ? Identical API endpoints to real SMS service
- ? Swagger UI for interactive testing
- ? Realistic behavior simulation (delays, failures)
- ? Message deduplication
- ? CORS enabled for web testing
- ? No actual SMS messages sent (safe for testing)

## ?? Testing

Use the included test scripts:
- 	est_mock_api.bat (Windows)
- 	est_mock_api.ps1 (PowerShell)

Or test manually:
```bash
# Send SMS
curl -X POST http://localhost:5555/api/sms/send \
  -H "Content-Type: application/json" \
  -d '{"message": "Test alarm", "groupId": 1, "priority": "normal"}'

# Check Status
curl http://localhost:5555/api/sms/status
```

## ?? Simulation Features

- **Processing Delay**: 100-500ms per message
- **Failure Rate**: 10% of messages fail (configurable)
- **Deduplication**: 5-minute window for duplicate detection
- **Queue Simulation**: Realistic queue size variations
- **Rate Limiting**: Tracked but not enforced

## ?? Important Note

This is a **MOCK SERVER** - no actual SMS messages are sent. Perfect for development, testing, and demonstrations without any real-world SMS costs or deliveries.
