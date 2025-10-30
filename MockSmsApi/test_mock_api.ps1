# Mock SMS API Server - Comprehensive Test Suite
# PowerShell version with better JSON handling

param(
    [string]$MockUrl = "http://localhost:5555"
)

$ApiUrl = "$MockUrl/api/sms"

Write-Host ""
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Mock SMS API Server - Comprehensive Test Suite" -ForegroundColor Cyan  
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Testing Mock SMS API endpoints..." -ForegroundColor Yellow
Write-Host "Mock server should be running at: $MockUrl" -ForegroundColor Yellow
Write-Host ""

# Function to make HTTP requests and display results
function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [int]$TestNumber,
        [int]$TotalTests
    )
    
    Write-Host "[$TestNumber/$TotalTests] Testing $Name..." -ForegroundColor White
    
    try {
        $headers = @{ "Content-Type" = "application/json" }
        
        if ($Method -eq "GET") {
            $response = Invoke-RestMethod -Uri $Url -Method $Method -Headers $headers
        } else {
            $jsonBody = $Body | ConvertTo-Json -Depth 10
            $response = Invoke-RestMethod -Uri $Url -Method $Method -Headers $headers -Body $jsonBody
        }
        
        Write-Host "? $Name successful" -ForegroundColor Green
        Write-Host ($response | ConvertTo-Json -Depth 10) -ForegroundColor Gray
        Write-Host ""
        
        return $response
    }
    catch {
        Write-Host "? $Name failed" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        
        if ($_.Exception.Response) {
            $statusCode = $_.Exception.Response.StatusCode.value__
            Write-Host "Status Code: $statusCode" -ForegroundColor Red
            
            try {
                $errorStream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($errorStream)
                $errorBody = $reader.ReadToEnd()
                if ($errorBody) {
                    Write-Host "Error Body: $errorBody" -ForegroundColor Red
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

# Test 1: Health Check
Test-Endpoint -Name "Health Check" -Method "GET" -Url "$ApiUrl/health" -TestNumber 1 -TotalTests 8

# Test 2: Service Status
Test-Endpoint -Name "Service Status" -Method "GET" -Url "$ApiUrl/status" -TestNumber 2 -TotalTests 8

# Test 3: Send Normal SMS
$normalSms = @{
    message = "Test alarm from SCADA system"
    groupId = 1
    alarmId = "TEST-001"
    priority = "normal"
}
Test-Endpoint -Name "Send SMS (Normal Priority)" -Method "POST" -Url "$ApiUrl/send" -Body $normalSms -TestNumber 3 -TotalTests 8

# Test 4: Send Urgent SMS
$urgentSms = @{
    message = "URGENT: Critical alarm detected!"
    groupId = 2
    alarmId = "TEST-002"
    priority = "urgent"
}
Test-Endpoint -Name "Send SMS (Urgent Priority)" -Method "POST" -Url "$ApiUrl/send" -Body $urgentSms -TestNumber 4 -TotalTests 8

# Test 5: Send Test SMS
$testSms = @{
    message = "This is a test message"
    groupId = 1
}
Test-Endpoint -Name "Send Test SMS" -Method "POST" -Url "$ApiUrl/test" -Body $testSms -TestNumber 5 -TotalTests 8

# Test 6: Legacy Root Endpoint  
$legacySms = @{
    message = "Legacy endpoint test"
    groupId = 3
    alarmId = "LEGACY-001"
}
Test-Endpoint -Name "Legacy Root Endpoint" -Method "POST" -Url "$MockUrl/" -Body $legacySms -TestNumber 6 -TotalTests 8

# Test 7: Legacy Status Endpoint
Test-Endpoint -Name "Legacy Status Endpoint" -Method "GET" -Url "$MockUrl/status" -TestNumber 7 -TotalTests 8

# Test 8: Error Handling (Invalid Request)
$invalidSms = @{
    message = ""
    groupId = 0
}
Write-Host "[8/8] Testing Error Handling..." -ForegroundColor White
try {
    $headers = @{ "Content-Type" = "application/json" }
    $jsonBody = $invalidSms | ConvertTo-Json
    $response = Invoke-RestMethod -Uri "$ApiUrl/send" -Method "POST" -Headers $headers -Body $jsonBody
    Write-Host "?? Expected error but got success:" -ForegroundColor Yellow
    Write-Host ($response | ConvertTo-Json -Depth 10) -ForegroundColor Gray
}
catch {
    Write-Host "? Error handling test completed (expected error)" -ForegroundColor Green
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "Status Code: $statusCode" -ForegroundColor Gray
        
        try {
            $errorStream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($errorStream)
            $errorBody = $reader.ReadToEnd()
            if ($errorBody) {
                Write-Host "Expected error response:" -ForegroundColor Gray
                Write-Host $errorBody -ForegroundColor Gray
            }
        }
        catch {
            # Ignore
        }
    }
}
Write-Host ""

# Final Status Check
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Final Status Check..." -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

try {
    $finalStatus = Invoke-RestMethod -Uri "$ApiUrl/status" -Method "GET" -Headers @{ "Content-Type" = "application/json" }
    Write-Host ($finalStatus | ConvertTo-Json -Depth 10) -ForegroundColor White
}
catch {
    Write-Host "? Final status check failed" -ForegroundColor Red
}

Write-Host ""
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Test Suite Complete!" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "All endpoints tested. Check responses above for any failures." -ForegroundColor White
Write-Host "Mock server stats should show increased message counts." -ForegroundColor White
Write-Host ""

Write-Host "To integrate with your SCADA system:" -ForegroundColor Yellow
Write-Host "1. Make sure appsettings.Development.json points to: $MockUrl" -ForegroundColor White
Write-Host "2. Run your SCADA SMS System in Development mode" -ForegroundColor White  
Write-Host "3. Use the SMS test page to verify integration" -ForegroundColor White
Write-Host ""

# Additional testing suggestions
Write-Host "Additional Testing Options:" -ForegroundColor Cyan
Write-Host "• Open browser to $MockUrl/swagger for interactive API testing" -ForegroundColor White
Write-Host "• Use your SCADA system's SMS Test page at /Test/Sms" -ForegroundColor White
Write-Host "• Check the mock server console for detailed request logs" -ForegroundColor White
Write-Host ""

Write-Host "Press any key to continue..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")