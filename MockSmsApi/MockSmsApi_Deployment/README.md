# Mock SMS API Server

This is a mock SMS API server that mimics the endpoints and behavior of your real SMS service. It's designed for testing your SCADA SMS System without sending actual SMS messages.

## Features

- **Same API endpoints** as your real SMS service
- **Realistic behavior** with simulated delays and occasional failures (10% failure rate)
- **Message deduplication** simulation
- **Rate limiting** tracking
- **Service status** monitoring
- **Swagger UI** for testing endpoints

## Quick Start

1. **Run the mock server:**
   ```bash
   cd MockSmsApi
   dotnet run
   ```

2. **The server will start on port 5555:**
   ```
   http://localhost:5555
   ```

3. **View API documentation:**
   ```
   http://localhost:5555/swagger
   ```

## Available Endpoints

### Legacy Compatibility
- `POST /` - Send SMS message (matches Python service)
- `GET /status` - Get service status

### Modern API Endpoints  
- `POST /api/sms/send` - Send SMS message
- `POST /api/sms/test` - Send test SMS message
- `GET /api/sms/status` - Get detailed service status
- `GET /api/sms/health` - Health check endpoint

## Configuration for Testing

To use this mock server with your SCADA SMS System, update your `appsettings.Development.json`:

```json
{
  "SmsSettings": {
    "ApiEndpoint": "http://localhost:5555",
    "ApiParams": "",
    "Username": "mock_user",
    "Password": "mock_password", 
    "SenderName": "MOCK-SCADA",
    "RateLimit": 10,
    "RateWindow": 60,
    "DuplicateWindow": 5
  }
}
```

## Testing Examples

### Send SMS via cURL
```bash
curl -X POST http://localhost:5555/api/sms/send \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Test alarm message",
    "groupId": 1,
    "alarmId": "TEST-123",
    "priority": "normal"
  }'
```

### Check Status
```bash
curl http://localhost:5555/api/sms/status
```

### Health Check
```bash
curl http://localhost:5555/api/sms/health
```

## Mock Behavior

- **Processing Delay:** 100-500ms random delay per message
- **Failure Rate:** 10% of messages will fail (simulated)
- **Deduplication:** Duplicate messages within 5 minutes are blocked
- **Queue Size:** Randomly varies between 0-10 messages
- **Rate Limiting:** Tracked but not enforced (for testing)

## Response Examples

### Successful SMS Send
```json
{
  "success": true,
  "message": "SMS queued successfully"
}
```

### Service Status
```json
{
  "queue_size": 3,
  "messages_sent": 42,
  "messages_failed": 5,
  "duplicates_blocked": 2,
  "rate_limited": 0,
  "service_uptime": "0.02:15:30",
  "last_message_time": "2024-01-15T14:30:45",
  "deduplication_enabled": true,
  "rate_limit": 10,
  "duplicate_window": 5
}
```

### Health Check
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T14:30:45.123Z",
  "queue_size": 3,
  "service_uptime": "0.02:15:30",
  "messages_sent_last_hour": 42,
  "deduplication_enabled": true,
  "details": {
    "rate_limit": 10,
    "duplicate_window": 5,
    "last_message_time": "2024-01-15T14:30:45",
    "mock_server": true,
    "failure_rate": "10%"
  }
}
```

## Integration with Your System

This mock server is designed to be a drop-in replacement for testing. Simply:

1. Start the mock server
2. Update your SMS API endpoint configuration to point to `http://localhost:5555`
3. Test your SCADA SMS System normally
4. Check the mock server logs to see all API calls

All endpoints return the same response formats as your real SMS service, making this perfect for development and testing scenarios.