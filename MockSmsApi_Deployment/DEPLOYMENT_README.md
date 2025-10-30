# Mock SMS API Server - Deployment Package

## ?? **NO .NET INSTALLATION REQUIRED!** ?

This is a **self-contained deployment** that includes the .NET 9 runtime. You can run it on any Windows machine without installing .NET first!

## ?? Quick Start

### Windows (Command Prompt) - **RECOMMENDED**
```cmd
start_server_selfcontained.bat
```

### Windows (PowerShell)
```powershell
.\start_server_selfcontained.ps1
```

### Alternative (if above don't work)
```cmd
start_server.bat
```

### Manual Execution
```bash
cd MockSmsApi
MockSmsApi.exe
```

## ?? Access Points

- **API Server**: http://localhost:5555
- **Swagger UI**: http://localhost:5555/swagger
- **Health Check**: http://localhost:5555/api/sms/health
- **Legacy Status**: http://localhost:5555/status

## ?? Available Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `POST /api/sms/send` | POST | Send SMS message |
| `POST /api/sms/test` | POST | Send test SMS |
| `GET /api/sms/status` | GET | Service status |
| `GET /api/sms/health` | GET | Health check |
| `POST /` | POST | Legacy SMS endpoint |
| `GET /status` | GET | Legacy status |

## ?? Requirements

- ? **Windows 10/11** (x64)
- ? **No .NET installation needed** (runtime included)
- ? **Port 5555 available**
- ? **No database required**
- ? **No external dependencies**

## ?? Features

- ? Identical API endpoints to real SMS service
- ? Swagger UI for interactive testing
- ? Realistic behavior simulation (delays, failures)
- ? Message deduplication
- ? CORS enabled for web testing
- ? No actual SMS messages sent (safe for testing)

## ?? Testing

Use the included test scripts:
- `test_mock_api.bat` (Windows)
- `test_mock_api.ps1` (PowerShell)

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

## ?? Package Information

- **Total Size**: 45.78 MB (ZIP) / 104.82 MB (extracted)
- **Runtime**: .NET 9 (included)
- **Platform**: Windows x64
- **Dependencies**: None (self-contained)

## ?? Important Note

This is a **MOCK SERVER** - no actual SMS messages are sent. Perfect for development, testing, and demonstrations without any real-world SMS costs or deliveries.

## ??? Troubleshooting

### Server Won't Start
1. **Check port availability**:
   ```cmd
   netstat -an | findstr :5555
   ```
2. **Run as Administrator** (if needed)
3. **Check Windows Firewall** settings
4. **Use alternative port**:
   ```cmd
   cd MockSmsApi
   MockSmsApi.exe --urls http://localhost:8080
   ```

### Access Issues
- Make sure you're accessing `http://localhost:5555` (not https)
- Try `http://127.0.0.1:5555` instead
- Check Windows Firewall isn't blocking the port

### Still Need .NET?
If you see ".NET not found" errors, the self-contained build may not have worked properly. You can:
1. Download .NET 9 Runtime from: https://dotnet.microsoft.com/download
2. Or use the regular `start_server.bat` script

## ?? Support

If you encounter issues:
1. Check the server console for error messages
2. Try the alternative startup scripts
3. Verify port 5555 is available
4. Run as Administrator if needed