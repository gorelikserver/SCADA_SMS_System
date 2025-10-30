# Mock SMS API - IAA AFCON Format Testing (PowerShell)

param(
    [string]$MockUrl = "http://localhost:5555"
)

Write-Host ""
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Mock SMS API - IAA AFCON Format Testing" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Testing with EXACT IAA AFCON SMS provider format:" -ForegroundColor Yellow
Write-Host "  • GET /services/SendMessage.asmx/SendMessagesReturenMessageID" -ForegroundColor Gray
Write-Host "  • Parameters in URL query string" -ForegroundColor Gray
Write-Host "  • Hebrew message support" -ForegroundColor Gray
Write-Host ""

# Function to test IAA AFCON format endpoints
function Test-IAAAfconSmsApi {
    param(
        [string]$Name,
        [hashtable]$QueryParams,
        [int]$TestNumber,
        [int]$TotalTests
    )
    
    Write-Host "[$TestNumber/$TotalTests] Testing $Name..." -ForegroundColor White
    
    try {
        # Build query string
        $queryString = ($QueryParams.GetEnumerator() | ForEach-Object { 
            "$($_.Key)=$([System.Web.HttpUtility]::UrlEncode($_.Value))" 
        }) -join "&"
        
        $fullUrl = "$MockUrl/services/SendMessage.asmx/SendMessagesReturenMessageID?$queryString"
        
        $response = Invoke-RestMethod -Uri $fullUrl -Method GET
        
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

# Test 1: Original IAA AFCON SMS Format
$iaaAfconSms = @{
    "UserName" = "d19afcsms"
    "Password" = "c5fe25896e49ddfe996db7508cf00534"
    "SenderName" = "IAA Afcon"
    "SendToPhoneNumbers" = "0546630841"
    "Message" = "????? 1"
    "CCToEmail" = ""
    "SMSOperation" = "Push"
    "DeliveryDelayInMinutes" = "0"
    "ExpirationDelayInMinutes" = "60"
    "MessageOption" = "Concatenated"
    "GroupCodes" = ""
    "Price" = "0"
}
Test-IAAAfconSmsApi -Name "IAA AFCON SMS Provider Format" -QueryParams $iaaAfconSms -TestNumber 1 -TotalTests 6

# Test 2: Hebrew Urgent Message
$hebrewUrgentSms = @{
    "UserName" = "d19afcsms"
    "Password" = "c5fe25896e49ddfe996db7508cf00534"
    "SenderName" = "IAA Afcon"
    "SendToPhoneNumbers" = "0507654321"
    "Message" = "URGENT: ????? ????? ??????"
    "SMSOperation" = "Push"
    "MessageOption" = "Concatenated"
    "CCToEmail" = ""
    "DeliveryDelayInMinutes" = "0"
    "ExpirationDelayInMinutes" = "60"
    "GroupCodes" = ""
    "Price" = "0"
}
Test-IAAAfconSmsApi -Name "Hebrew Urgent Message" -QueryParams $hebrewUrgentSms -TestNumber 2 -TotalTests 6

# Test 3: Missing Parameters (Error Case)
Write-Host "[3/6] Testing Missing Parameters (Error Case)..." -ForegroundColor White
try {
    $errorParams = @{
        "UserName" = "test"
        "Password" = "test"
    }
    $queryString = ($errorParams.GetEnumerator() | ForEach-Object { 
        "$($_.Key)=$([System.Web.HttpUtility]::UrlEncode($_.Value))" 
    }) -join "&"
    
    $fullUrl = "$MockUrl/services/SendMessage.asmx/SendMessagesReturenMessageID?$queryString"
    $response = Invoke-RestMethod -Uri $fullUrl -Method GET
    Write-Host "?? Expected error but got success: $response" -ForegroundColor Yellow
}
catch {
    Write-Host "? Error handling test completed (expected error)" -ForegroundColor Green
    if ($_.Exception.Response) {
        Write-Host "Expected error response received" -ForegroundColor Gray
    }
}
Write-Host ""

# Test 4: Modern API Endpoint with IAA AFCON Format
$modernAfcon = @{
    "UserName" = "d19afcsms"
    "Password" = "c5fe25896e49ddfe996db7508cf00534"
    "SenderName" = "IAA Afcon"
    "SendToPhoneNumbers" = "0546630841"
    "Message" = "Test via modern endpoint"
    "SMSOperation" = "Push"
    "MessageOption" = "Concatenated"
}

Write-Host "[4/6] Testing Modern IAA AFCON Endpoint..." -ForegroundColor White
try {
    $queryString = ($modernAfcon.GetEnumerator() | ForEach-Object { 
        "$($_.Key)=$([System.Web.HttpUtility]::UrlEncode($_.Value))" 
    }) -join "&"
    
    $response = Invoke-RestMethod -Uri "$MockUrl/api/sms/send-iaa-afcon?$queryString" -Method GET
    Write-Host "? Modern IAA AFCON endpoint successful" -ForegroundColor Green
    Write-Host "Response: $response" -ForegroundColor Gray
}
catch {
    Write-Host "? Modern endpoint test failed" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

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
Write-Host "IAA AFCON FORMAT TESTING COMPLETE" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Your SCADA system should now work with the mock server" -ForegroundColor Green
Write-Host "using the exact same API format as IAA AFCON SMS provider!" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration for appsettings.json:" -ForegroundColor Cyan
Write-Host ""
@"
{
  "SmsSettings": {
    "ApiEndpoint": "http://localhost:5555/services/SendMessage.asmx/SendMessagesReturenMessageID",
    "ApiParams": "{\"UserName\": \"username\", \"Password\": \"password\", \"SenderName\": \"sender_name\", \"SendToPhoneNumbers\": \"phone\", \"Message\": \"message\", \"CCToEmail\": \"\", \"SMSOperation\": \"Push\", \"DeliveryDelayInMinutes\": \"0\", \"ExpirationDelayInMinutes\": \"60\", \"MessageOption\": \"Concatenated\", \"GroupCodes\": \"\", \"Price\": \"0\"}",
    "Username": "d19afcsms",
    "Password": "c5fe25896e49ddfe996db7508cf00534",
    "SenderName": "IAA Afcon"
  }
}
"@ | Write-Host -ForegroundColor White

Write-Host ""
Write-Host "?? Test Commands:" -ForegroundColor Cyan
Write-Host "  PowerShell: .\test_iaa_afcon_format.ps1" -ForegroundColor White
Write-Host "  Batch:      test_iaa_afcon_format.bat" -ForegroundColor White
Write-Host "  Swagger UI: http://localhost:5555/swagger" -ForegroundColor White
Write-Host ""

Write-Host "Press any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")