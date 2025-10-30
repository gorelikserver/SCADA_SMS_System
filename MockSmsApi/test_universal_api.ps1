# Universal Mock SMS API Testing (PowerShell)

param(
    [string]$MockUrl = "http://localhost:5555"
)

Write-Host ""
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Universal Mock SMS API - Complete Testing" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Testing UNIVERSAL SMS API that works with ANY provider format:" -ForegroundColor Yellow
Write-Host "  • Any HTTP Method (GET, POST, PUT, PATCH)" -ForegroundColor Gray
Write-Host "  • Any Content Type (JSON, Form, Query Parameters)" -ForegroundColor Gray
Write-Host "  • Any Endpoint Path" -ForegroundColor Gray
Write-Host "  • Any Parameter Names" -ForegroundColor Gray
Write-Host ""

function Test-UniversalSmsApi {
    param(
        [string]$TestName,
        [string]$Method,
        [string]$Endpoint,
        [hashtable]$Parameters = @{},
        [string]$ContentType = "application/x-www-form-urlencoded",
        [int]$TestNumber,
        [int]$TotalTests
    )
    
    Write-Host "[$TestNumber/$TotalTests] Testing $TestName..." -ForegroundColor White
    
    try {
        $fullUrl = "$MockUrl$Endpoint"
        
        if ($Method -eq "GET" -and $Parameters.Count -gt 0) {
            # Add parameters to query string for GET
            $queryString = ($Parameters.GetEnumerator() | ForEach-Object { 
                "$($_.Key)=$([System.Web.HttpUtility]::UrlEncode($_.Value))" 
            }) -join "&"
            $fullUrl += "?$queryString"
            
            $response = Invoke-RestMethod -Uri $fullUrl -Method $Method
        }
        elseif ($ContentType.Contains("json") -and $Parameters.Count -gt 0) {
            # JSON content for POST/PUT
            $jsonBody = $Parameters | ConvertTo-Json
            $response = Invoke-RestMethod -Uri $fullUrl -Method $Method -Body $jsonBody -ContentType $ContentType
        }
        elseif ($Parameters.Count -gt 0) {
            # Form data for POST/PUT
            $formData = ($Parameters.GetEnumerator() | ForEach-Object { 
                "$($_.Key)=$([System.Web.HttpUtility]::UrlEncode($_.Value))" 
            }) -join "&"
            $response = Invoke-RestMethod -Uri $fullUrl -Method $Method -Body $formData -ContentType $ContentType
        }
        else {
            # No parameters
            $response = Invoke-RestMethod -Uri $fullUrl -Method $Method
        }
        
        Write-Host "? $TestName successful" -ForegroundColor Green
        Write-Host "Response: $response" -ForegroundColor Gray
        Write-Host ""
        
        return $true
    }
    catch {
        Write-Host "? $TestName failed" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        
        if ($_.Exception.Response) {
            try {
                $errorStream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($errorStream)
                $errorBody = $reader.ReadToEnd()
                if ($errorBody) {
                    Write-Host "Response: $errorBody" -ForegroundColor Red
                }
            }
            catch {
                # Ignore errors reading error response
            }
        }
        Write-Host ""
        return $false
    }
}

# Add System.Web for URL encoding
Add-Type -AssemblyName System.Web

$successCount = 0
$totalTests = 8

# Test 1: IAA AFCON Format (GET with query parameters)
$iaaAfconParams = @{
    "UserName" = "d19afcsms"
    "Password" = "test123"
    "SenderName" = "IAA Afcon"
    "SendToPhoneNumbers" = "0546630841"
    "Message" = "????? ?????"
    "SMSOperation" = "Push"
}
if (Test-UniversalSmsApi -TestName "IAA AFCON Format (GET)" -Method "GET" -Endpoint "/services/SendMessage.asmx/SendMessagesReturenMessageID" -Parameters $iaaAfconParams -TestNumber 1 -TotalTests $totalTests) {
    $successCount++
}

# Test 2: Generic SMS Provider (POST form-urlencoded)
$genericParams = @{
    "phone" = "+1234567890"
    "message" = "Hello World"
    "username" = "testuser"
    "password" = "testpass"
}
if (Test-UniversalSmsApi -TestName "Generic POST Form" -Method "POST" -Endpoint "/send.php" -Parameters $genericParams -TestNumber 2 -TotalTests $totalTests) {
    $successCount++
}

# Test 3: Modern API (POST JSON)
$jsonParams = @{
    "mobile" = "+972501234567"
    "text" = "JSON message test"
    "user" = "api_user"
}
if (Test-UniversalSmsApi -TestName "Modern JSON API" -Method "POST" -Endpoint "/api/v1/sms" -Parameters $jsonParams -ContentType "application/json" -TestNumber 3 -TotalTests $totalTests) {
    $successCount++
}

# Test 4: Simple GET with minimal parameters
$simpleParams = @{
    "number" = "555-0123"
    "msg" = "Simple test"
}
if (Test-UniversalSmsApi -TestName "Simple GET" -Method "GET" -Endpoint "/sms.aspx" -Parameters $simpleParams -TestNumber 4 -TotalTests $totalTests) {
    $successCount++
}

# Test 5: Legacy provider format
$legacyParams = @{
    "mobilenumber" = "123456789"
    "body" = "Legacy message"
    "from" = "SCADA"
}
if (Test-UniversalSmsApi -TestName "Legacy Format" -Method "POST" -Endpoint "/sendsms" -Parameters $legacyParams -TestNumber 5 -TotalTests $totalTests) {
    $successCount++
}

# Test 6: PUT method with JSON
$putParams = @{
    "phone" = "987654321"
    "content" = "PUT method test"
    "sender" = "TEST"
}
if (Test-UniversalSmsApi -TestName "PUT JSON Method" -Method "PUT" -Endpoint "/sms/send" -Parameters $putParams -ContentType "application/json" -TestNumber 6 -TotalTests $totalTests) {
    $successCount++
}

# Test 7: Missing parameters (error case)
$errorParams = @{
    "user" = "testonly"
}
Write-Host "[7/$totalTests] Testing Missing Parameters (Error Case)..." -ForegroundColor White
try {
    $response = Invoke-RestMethod -Uri "$MockUrl/test?user=testonly" -Method GET
    Write-Host "?? Expected error but got: $response" -ForegroundColor Yellow
}
catch {
    Write-Host "? Error handling working (expected error)" -ForegroundColor Green
    $successCount++
}
Write-Host ""

# Test 8: Status endpoint
Write-Host "[8/$totalTests] Testing Status Endpoint..." -ForegroundColor White
try {
    $status = Invoke-RestMethod -Uri "$MockUrl/status" -Method GET
    Write-Host "? Status endpoint working" -ForegroundColor Green
    Write-Host ($status | ConvertTo-Json -Depth 3) -ForegroundColor Gray
    $successCount++
}
catch {
    Write-Host "? Status endpoint failed" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "UNIVERSAL API TESTING COMPLETE" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Test Results: $successCount/$totalTests tests passed" -ForegroundColor $(if ($successCount -eq $totalTests) { "Green" } else { "Yellow" })
Write-Host ""
Write-Host "?? The Universal Mock SMS API can handle:" -ForegroundColor Green
Write-Host "? GET requests with query parameters" -ForegroundColor White
Write-Host "? POST requests with form data" -ForegroundColor White
Write-Host "? POST/PUT requests with JSON" -ForegroundColor White
Write-Host "? Any endpoint path" -ForegroundColor White
Write-Host "? Flexible parameter names" -ForegroundColor White
Write-Host "? Hebrew/Unicode messages" -ForegroundColor White
Write-Host "? Realistic error simulation" -ForegroundColor White
Write-Host ""

Write-Host "?? Configuration Examples:" -ForegroundColor Cyan
Write-Host ""

Write-Host "For IAA AFCON (current):" -ForegroundColor Yellow
@"
{
  "SmsSettings": {
    "HttpMethod": "GET",
    "ApiEndpoint": "http://localhost:5555/services/SendMessage.asmx/SendMessagesReturenMessageID",
    "ContentType": "application/x-www-form-urlencoded",
    "ApiParams": "{\\"UserName\\": \\"username\\", \\"SendToPhoneNumbers\\": \\"phone\\", \\"Message\\": \\"message\\"}"
  }
}
"@ | Write-Host -ForegroundColor White

Write-Host ""
Write-Host "For JSON API:" -ForegroundColor Yellow
@"
{
  "SmsSettings": {
    "HttpMethod": "POST",
    "ApiEndpoint": "http://localhost:5555/api/sms/send",
    "ContentType": "application/json",
    "ApiParams": "{\\"mobile\\": \\"phone\\", \\"text\\": \\"message\\", \\"user\\": \\"username\\"}"
  }
}
"@ | Write-Host -ForegroundColor White

Write-Host ""
Write-Host "For Simple GET:" -ForegroundColor Yellow
@"
{
  "SmsSettings": {
    "HttpMethod": "GET",
    "ApiEndpoint": "http://localhost:5555/send",
    "ApiParams": "{\\"number\\": \\"phone\\", \\"msg\\": \\"message\\"}"
  }
}
"@ | Write-Host -ForegroundColor White

Write-Host ""
Write-Host "Press any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")