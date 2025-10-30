# Mock SMS API - Original Format Testing (PowerShell)

param(
    [string]$MockUrl = "http://localhost:5555"
)

Write-Host ""
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Mock SMS API - ORIGINAL FORMAT Testing" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Testing with EXACT original SMS provider format:" -ForegroundColor Yellow
Write-Host "  • POST application/x-www-form-urlencoded" -ForegroundColor Gray
Write-Host "  • Parameters: SendToPhoneNumbers, Message, UserName, Password, SenderName" -ForegroundColor Gray
Write-Host ""

# Function to test original format endpoints
function Test-OriginalSmsApi {
    param(
        [string]$Name,
        [string]$Url,
        [hashtable]$FormData,
        [int]$TestNumber,
        [int]$TotalTests
    )
    
    Write-Host "[$TestNumber/$TotalTests] Testing $Name..." -ForegroundColor White
    
    try {
        # Build form data string
        $formString = ($FormData.GetEnumerator() | ForEach-Object { 
            "$($_.Key)=$([System.Web.HttpUtility]::UrlEncode($_.Value))" 
        }) -join "&"
        
        $headers = @{ 
            "Content-Type" = "application/x-www-form-urlencoded"
        }
        
        $response = Invoke-RestMethod -Uri $Url -Method POST -Headers $headers -Body $formString
        
        Write-Host "? $Name successful" -ForegroundColor Green
        Write-Host "Response: $response" -ForegroundColor Gray
        Write-Host ""
        
        return $response
    }
    catch {
        Write-Host "? $Name failed" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        
        if ($_.Exception.Response) {
            try {
                $errorStream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($errorStream)
                $errorBody = $reader.ReadToEnd()
                if ($errorBody) {
                    Write-Host "Response Body: $errorBody" -ForegroundColor Red
                }
            }
            catch {
                # Ignore errors reading error response
            }
        }
        Write-Host ""
        return $null
    }
}

# Add System.Web for URL encoding
Add-Type -AssemblyName System.Web

# Test 1: Original SMS Format
$originalSms = @{
    "SendToPhoneNumbers" = "+972501234567"
    "Message" = "Test alarm from SCADA system"
    "UserName" = "mock_user"
    "Password" = "mock_password"
    "SenderName" = "SCADA"
}
Test-OriginalSmsApi -Name "Original SMS Provider Format" -Url "$MockUrl/" -FormData $originalSms -TestNumber 1 -TotalTests 6

# Test 2: Urgent Message
$urgentSms = @{
    "SendToPhoneNumbers" = "+972507654321"
    "Message" = "URGENT: Critical equipment failure"
    "UserName" = "scada_system"
    "Password" = "your_password"
    "SenderName" = "SCADA-ALERT"
}
Test-OriginalSmsApi -Name "Urgent Message Format" -Url "$MockUrl/" -FormData $urgentSms -TestNumber 2 -TotalTests 6

# Test 3: Missing Parameters (Error Case)
Write-Host "[3/6] Testing Missing Parameters (Error Case)..." -ForegroundColor White
try {
    $errorSms = @{
        "UserName" = "test"
        "Password" = "test"
    }
    $formString = ($errorSms.GetEnumerator() | ForEach-Object { 
        "$($_.Key)=$([System.Web.HttpUtility]::UrlEncode($_.Value))" 
    }) -join "&"
    
    $headers = @{ "Content-Type" = "application/x-www-form-urlencoded" }
    $response = Invoke-RestMethod -Uri "$MockUrl/" -Method POST -Headers $headers -Body $formString
    Write-Host "?? Expected error but got success: $response" -ForegroundColor Yellow
}
catch {
    Write-Host "? Error handling test completed (expected error)" -ForegroundColor Green
    if ($_.Exception.Response) {
        try {
            $errorStream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorStream)
            $errorBody = $reader.ReadToEnd()
            Write-Host "Expected error response: $errorBody" -ForegroundColor Gray
        }
        catch {
            # Ignore
        }
    }
}
Write-Host ""

# Test 4: Modern API Endpoint with Original Format
$modernSms = @{
    "SendToPhoneNumbers" = "+972501234567"
    "Message" = "Test via modern endpoint"
    "UserName" = "mock_user"
    "Password" = "mock_password"
    "SenderName" = "SCADA"
}
Test-OriginalSmsApi -Name "Modern Original Format Endpoint" -Url "$MockUrl/api/sms/send-original" -FormData $modernSms -TestNumber 4 -TotalTests 6

# Test 5: Status Endpoint
Write-Host "[5/6] Testing Status Endpoint..." -ForegroundColor White
try {
    $status = Invoke-RestMethod -Uri "$MockUrl/status" -Method GET
    Write-Host "? Status endpoint successful" -ForegroundColor Green
    Write-Host ($status | ConvertTo-Json -Depth 3) -ForegroundColor Gray
}
catch {
    Write-Host "? Status endpoint failed" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 6: Health Endpoint
Write-Host "[6/6] Testing Health Endpoint..." -ForegroundColor White
try {
    $health = Invoke-RestMethod -Uri "$MockUrl/api/sms/health" -Method GET
    Write-Host "? Health endpoint successful" -ForegroundColor Green
    Write-Host ($health | ConvertTo-Json -Depth 3) -ForegroundColor Gray
}
catch {
    Write-Host "? Health endpoint failed" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "ORIGINAL FORMAT TESTING COMPLETE" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Your SCADA system should now work with the mock server" -ForegroundColor Green
Write-Host "using the exact same API format as your real SMS provider!" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration for appsettings.Development.json:" -ForegroundColor Cyan
Write-Host ""
@"
{
  "SmsSettings": {
    "ApiEndpoint": "http://localhost:5555",
    "ApiParams": "{\"SendToPhoneNumbers\": \"phone\", \"Message\": \"message\", \"UserName\": \"username\", \"Password\": \"password\", \"SenderName\": \"sender_name\"}",
    "Username": "mock_user",
    "Password": "mock_password",
    "SenderName": "SCADA"
  }
}
"@ | Write-Host -ForegroundColor White

Write-Host ""
Write-Host "?? Test Commands:" -ForegroundColor Cyan
Write-Host "  PowerShell: .\test_original_format.ps1" -ForegroundColor White
Write-Host "  Batch:      test_original_format.bat" -ForegroundColor White
Write-Host "  Swagger UI: http://localhost:5555/swagger" -ForegroundColor White
Write-Host ""

Write-Host "Press any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")