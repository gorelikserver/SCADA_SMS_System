#Requires -Version 5.1
<#
.SYNOPSIS
    Build a versioned MSI release for SCADA SMS System.
.PARAMETER Version
    Semantic version for this release, e.g. 2.3.0
.EXAMPLE
    .\build-release.ps1 -Version 2.3.0
#>
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$Root    = $PSScriptRoot
$Staging = "$Root\deployment_package\InstallerSrc"
$AppOut  = "$Root\deployment_package\Application"

function Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Ok([string]$msg)   { Write-Host "    OK  $msg" -ForegroundColor Green }
function Die([string]$msg)  { Write-Host "`nERROR: $msg" -ForegroundColor Red; exit 1 }

# ── 1. Bump version in Variables.wxi ─────────────────────────────────────────
Step "Bumping version to $Version"

$wxi = "$Root\Installer\Variables.wxi"
$raw = Get-Content $wxi -Raw
$raw = $raw -replace '(?<=\bProductVersion\b\s*=\s*")[^"]*', $Version
Set-Content $wxi $raw -Encoding UTF8
Ok "Variables.wxi → $Version"

# ── 2. Clean previous artifacts ───────────────────────────────────────────────
Step "Cleaning previous build artifacts"
@($AppOut, $Staging) | ForEach-Object {
    if (Test-Path $_) { Remove-Item $_ -Recurse -Force }
}
Ok "Cleaned deployment_package"

# ── 3. dotnet publish (self-contained, win-x64) ───────────────────────────────
Step "Publishing application"
$pub = & dotnet publish "$Root\SCADASMSSystem.Web.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $AppOut `
    --verbosity quiet 2>&1

if ($LASTEXITCODE -ne 0) { Die "dotnet publish failed:`n$pub" }
Ok "Published to deployment_package\Application"

# ── 4. Stage installer source ─────────────────────────────────────────────────
Step "Staging installer source"

New-Item -ItemType Directory -Path "$Staging\Main" -Force | Out-Null

# Copy all published files into Main/
Copy-Item "$AppOut\*" "$Staging\Main\" -Recurse -Force

# Separate the two files WiX references directly (avoid duplicate component errors)
Move-Item "$Staging\Main\SCADASMSSystem.Web.exe" "$Staging\" -Force
Move-Item "$Staging\Main\appsettings.json"        "$Staging\" -Force

Ok "Staged to deployment_package\InstallerSrc"

# ── 5. Generate harvested file list ───────────────────────────────────────────
Step "Harvesting installer file list"

$harvestOut = "$Root\Installer\obj\Release\HarvestedFiles.wxs"
New-Item -ItemType Directory -Path (Split-Path $harvestOut) -Force | Out-Null

& "$Root\Installer\Scripts\Generate-HarvestedFiles.ps1" `
    -SourceDir "$Staging\Main" `
    -OutputFile $harvestOut

if ($LASTEXITCODE -ne 0) { Die "Harvest script failed" }
Ok "HarvestedFiles.wxs generated"

# ── 6. Build MSI ──────────────────────────────────────────────────────────────
Step "Building MSI"

# Stamp OutputName with new version so MSI filename matches
$wixproj = "$Root\Installer\SCADASMSInstaller.wixproj"
$proj = Get-Content $wixproj -Raw
$proj = $proj -replace '(?<=<OutputName>)[^<]*', "SCADA_SMS_v${Version}_Setup"
$proj = $proj -replace '(?<=<ProductVersion>)[^<]*', $Version
Set-Content $wixproj $proj -Encoding UTF8

Push-Location "$Root\Installer"
$build = & dotnet build SCADASMSInstaller.wixproj `
    --configuration Release `
    --verbosity normal 2>&1
Pop-Location

$build | Write-Host
if ($LASTEXITCODE -ne 0) { Die "MSI build failed" }

$msi = Get-ChildItem "$Root\Installer\bin\x64\Release\*.msi" | Select-Object -First 1
if (-not $msi) { Die "MSI not found after build" }

# Copy to repo root for easy access
$dest = "$Root\SCADA_SMS_v${Version}_Setup.msi"
Copy-Item $msi.FullName $dest -Force
Ok "MSI → $dest"

# ── Done ──────────────────────────────────────────────────────────────────────
Write-Host "`n$('─' * 60)" -ForegroundColor DarkGray
Write-Host "  Release $Version built successfully" -ForegroundColor Green
Write-Host "  $dest" -ForegroundColor White
Write-Host "$('─' * 60)`n" -ForegroundColor DarkGray
