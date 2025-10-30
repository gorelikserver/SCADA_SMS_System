# IAA AFCON SMS Mock Server

## Air-Gapped Deployment

This is a clean, minimal implementation of the IAA AFCON SMS provider mock server for air-gapped environments.

### Files Included

- `Program.cs` - Main application code
- `MockSmsApi.csproj` - Project configuration
- `appsettings.json` - Production configuration
- `appsettings.Development.json` - Development configuration
- `start.bat` - Windows startup script
- `start.sh` - Linux/macOS startup script
- `build.bat` - Build self-contained executable

### Quick Start

#### Option 1: Run with .NET Runtime
1. Ensure .NET 9.0 runtime is installed
2. Run: `start.bat` (Windows) or `./start.sh` (Linux/macOS)

#### Option 2: Build Self-Contained Executable (No .NET Required)
1. Run: `build.bat`
2. Copy the entire `publish` folder to target machine
3. Run: `publish\MockSmsApi.exe`

### API Endpoint

The mock server provides the exact IAA AFCON SMS API endpoint:

```
GET /services/SendMessage.asmx/SendMessagesReturenMessageID
```

**Required Parameters:**
- UserName (validates: `d19afcsms`)
- Password
- SendToPhoneNumbers
- Message

**Optional Parameters:**
- SenderName, CCToEmail, SMSOperation, DeliveryDelayInMinutes, ExpirationDelayInMinutes, MessageOption, GroupCodes, Price

### Monitoring Endpoints

- `/status` - Service status and statistics
- `/health` - Health check
- `/` - Service information

### Configuration

Default port: `5555`

To change port, modify `appsettings.json`:
```json
{
  "Urls": "http://localhost:YOUR_PORT"
}
```

### Air-Gapped Environment

This package contains everything needed for offline deployment:
- No external dependencies
- Self-contained .NET runtime (when built)
- Minimal resource requirements
- No internet connectivity required