<#
.SYNOPSIS
    Runs the pre-site-integration mock suite (scenarios + alarm-storm load) and writes the report.

.DESCRIPTION
    1. Builds the solution.
    2. Runs the existing unit tests and the mock self-tests.
    3. Runs the end-to-end scenarios (Category=Scenario) and the load tests (Category=Load) with
       SCADA_MOCK_IT=1. They start the real web app as a separate process against the dedicated
       scada_mock_it database on the local SQL Server, fire Scada_sms.bat through cmd.exe and talk
       only to the in-process loopback mock provider. No real SMS is ever sent.
    4. Generates report.html (offline, self-contained) and summary.md.

    Output: test-results\mock-integration\<yyyyMMdd-HHmmss>\ (gitignored).

.PARAMETER Configuration
    Build configuration (default Debug).
.PARAMETER SkipScenarios
    Do not run the scenario tests.
.PARAMETER SkipLoad
    Do not run the load tests.
.PARAMETER Filter
    Extra dotnet test filter appended to the scenario and load filters (e.g. "FullyQualifiedName~Soap").
.PARAMETER SqlConnection
    Connection string for the test SQL Server. Default: appsettings.json DefaultConnection; the
    database name is always forced to scada_mock_it.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tests\integration\run-mock-suite.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$SkipScenarios,
    [switch]$SkipLoad,
    [string]$Filter = '',
    [string]$SqlConnection = ''
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $repo "test-results\mock-integration\$stamp"
$trx = Join-Path $out 'trx'
New-Item -ItemType Directory -Force -Path $trx | Out-Null
$dotnet = if ($env:DOTNET_EXE) { $env:DOTNET_EXE } else { 'dotnet' }
$sln = Join-Path $repo 'SCADASMSSystem.Web.sln'
$itProject = Join-Path $repo 'tests\SCADASMSSystem.IntegrationTests\SCADASMSSystem.IntegrationTests.csproj'
$unitProject = Join-Path $repo 'tests\SCADASMSSystem.Tests\SCADASMSSystem.Tests.csproj'
$reportProject = Join-Path $repo 'tests\SCADASMSSystem.MockReport\SCADASMSSystem.MockReport.csproj'
$durations = [ordered]@{}
$failures = @()

function Invoke-Phase([string]$name, [scriptblock]$body) {
    Write-Host "=== $name ===" -ForegroundColor Cyan
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & $body
    $code = $LASTEXITCODE
    $sw.Stop()
    $durations[$name] = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    if ($code -ne 0) {
        Write-Host "$name exited with $code" -ForegroundColor Yellow
        $script:failures += "$name (exit $code)"
    }
}

function Join-Filter([string]$baseFilter) {
    if ($Filter) { return "($baseFilter)&($Filter)" } else { return $baseFilter }
}

Invoke-Phase 'build' { & $dotnet build $sln -c $Configuration -nologo -v:minimal 2>&1 | Tee-Object -FilePath (Join-Path $out 'build.log') | Select-Object -Last 5 }
if ($failures.Count -gt 0) { throw "Build failed; see $out\build.log" }

Invoke-Phase 'unit-tests' { & $dotnet test $unitProject -c $Configuration --no-build --logger 'trx;LogFileName=unit-tests.trx' --results-directory $trx 2>&1 | Select-Object -Last 3 }
Invoke-Phase 'mock-self-tests' { & $dotnet test $itProject -c $Configuration --no-build --filter 'Category=MockSelfTest' --logger 'trx;LogFileName=mock-self-tests.trx' --results-directory $trx 2>&1 | Select-Object -Last 3 }

$env:SCADA_MOCK_IT = '1'
$env:SCADA_MOCK_IT_RESULTS = $out
if ($SqlConnection) { $env:SCADA_MOCK_IT_SQL = $SqlConnection }

if (-not $SkipScenarios) {
    $f = Join-Filter 'Category=Scenario'
    Invoke-Phase 'scenarios' { & $dotnet test $itProject -c $Configuration --no-build --filter $f --logger 'trx;LogFileName=scenarios.trx' --logger 'console;verbosity=normal' --results-directory $trx 2>&1 | Tee-Object -FilePath (Join-Path $out 'scenarios.log') | Where-Object { $_ -match '^\s+(Passed|Failed|Skipped) |Total tests|Passed!|Failed!' } }
}
if (-not $SkipLoad) {
    $f = Join-Filter 'Category=Load'
    Invoke-Phase 'load' { & $dotnet test $itProject -c $Configuration --no-build --filter $f --logger 'trx;LogFileName=load.trx' --logger 'console;verbosity=normal' --results-directory $trx 2>&1 | Tee-Object -FilePath (Join-Path $out 'load.log') | Where-Object { $_ -match '^\s+(Passed|Failed|Skipped) |Total tests|Passed!|Failed!' } }
}

# Environment for the report (no secrets: only the SQL server name and version).
$sqlServer = ''
$sqlVersion = ''
try {
    $cs = if ($SqlConnection) { $SqlConnection } else { (Get-Content (Join-Path $repo 'appsettings.json') -Raw | ConvertFrom-Json).ConnectionStrings.DefaultConnection }
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $cs
    $sqlServer = $builder.DataSource
    $builder.InitialCatalog = 'master'
    $conn = New-Object System.Data.SqlClient.SqlConnection $builder.ConnectionString
    $conn.Open()
    $cmd = $conn.CreateCommand(); $cmd.CommandText = "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) + ' ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(100))"
    $sqlVersion = $cmd.ExecuteScalar()
    $conn.Close()
} catch { $sqlVersion = "unknown ($($_.Exception.Message))" }

$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$envInfo = [ordered]@{
    generatedAt     = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz')
    machine         = $env:COMPUTERNAME
    os              = "$($os.Caption) $($os.Version)"
    cpu             = "$($cpu.Name.Trim()) ($([Environment]::ProcessorCount) logical)"
    memoryGB        = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
    dotnetSdk       = (& $dotnet --version)
    configuration   = $Configuration
    gitSha          = (& git -C $repo rev-parse --short HEAD)
    gitBranch       = (& git -C $repo rev-parse --abbrev-ref HEAD)
    sqlServer       = $sqlServer
    sqlVersion      = $sqlVersion
    testDatabase    = 'scada_mock_it'
    curl            = ((& curl.exe --version 2>$null) | Select-Object -First 1)
    powershell      = $PSVersionTable.PSVersion.ToString()
    batFile         = 'Scada_sms.bat (byte-identical copy per site, Scada_sms.ini with URL/SPOOL_ROOT/MAXTIME)'
    phaseSeconds    = $durations
    phaseFailures   = $failures
}
$envInfo | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $out 'env.json')

Invoke-Phase 'report' { & $dotnet run --project $reportProject -c $Configuration --no-build -- $out }

Write-Host ''
Write-Host "Report:  $out\report.html" -ForegroundColor Green
Write-Host "Summary: $out\summary.md" -ForegroundColor Green
if ($failures.Count -gt 0) {
    Write-Host ("Phases with failures: " + ($failures -join ', ')) -ForegroundColor Yellow
    exit 1
}
exit 0
