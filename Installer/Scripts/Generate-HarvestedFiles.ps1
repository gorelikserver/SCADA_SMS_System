<#
.SYNOPSIS
  Pre-build harvest script for WiX v4. Generates a ComponentGroup wxs from all
  files in the staged InstallerSrc\Main directory.
  Called by SCADASMSInstaller.wixproj BeforeCompile target.
#>
param(
    [Parameter(Mandatory)][string]$SourceDir,
    [Parameter(Mandatory)][string]$OutputFile
)

$SourceDir = (Resolve-Path $SourceDir).Path.TrimEnd('\')

function Get-DeterministicGuid([string]$seed) {
    $sha1    = [System.Security.Cryptography.SHA1]::Create()
    $bytes   = [System.Text.Encoding]::UTF8.GetBytes($seed.ToLowerInvariant())
    $hash    = $sha1.ComputeHash($bytes)
    $hex     = ($hash[0..15] | ForEach-Object { $_.ToString("x2") }) -join ""
    return "$($hex.Substring(0,8))-$($hex.Substring(8,4))-$($hex.Substring(12,4))-$($hex.Substring(16,4))-$($hex.Substring(20,12))"
}

function Safe-Id([string]$path) {
    $clean = "f_" + ($path -replace '[^a-zA-Z0-9]', '_').TrimEnd('_')
    if ($clean.Length -le 60) { return $clean }
    # Truncate long names: keep prefix + hash suffix
    $sha1    = [System.Security.Cryptography.SHA1]::Create()
    $bytes   = [System.Text.Encoding]::UTF8.GetBytes($path.ToLowerInvariant())
    $hash    = $sha1.ComputeHash($bytes)
    $suffix  = "h" + (($hash[0..3] | ForEach-Object { $_.ToString("x2") }) -join "")
    return $clean.Substring(0, 52) + "_" + $suffix
}

function Dir-Id([string]$relPath) {
    if (-not $relPath) { return "INSTALLDIR" }
    return "d_" + ($relPath -replace '[^a-zA-Z0-9]', '_').TrimEnd('_')
}

# Collect all files
$allFiles  = Get-ChildItem -Recurse -File $SourceDir
$allDirs   = Get-ChildItem -Recurse -Directory $SourceDir | Sort-Object FullName

$sb = [System.Text.StringBuilder]::new()

[void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')

# ---- Directory tree rooted at INSTALLDIR ----
[void]$sb.AppendLine('    <DirectoryRef Id="INSTALLDIR">')

# Build directory hierarchy
$dirTree = @{}
foreach ($dir in $allDirs) {
    $rel  = $dir.FullName.Substring($SourceDir.Length).TrimStart('\')
    $parts = $rel -split '\\'
    $dirTree[$rel] = @{ parts = $parts; dir = $dir }
}

# Output nested directory elements using a stack approach
# Flatten to sorted list and emit with indentation
function Write-Dirs([string]$parentRel, [int]$indent) {
    $prefix = ' ' * $indent
    $children = $allDirs | Where-Object {
        $rel = $_.FullName.Substring($SourceDir.Length).TrimStart('\')
        $parentParts = if ($parentRel) { ($parentRel -split '\\').Count } else { 0 }
        $relParts    = ($rel -split '\\').Count
        if ($parentRel) {
            $rel.StartsWith($parentRel + '\') -and $relParts -eq ($parentParts + 1)
        } else {
            $relParts -eq 1
        }
    }
    foreach ($child in $children) {
        $childRel = $child.FullName.Substring($SourceDir.Length).TrimStart('\')
        $childId  = Dir-Id $childRel
        [void]$sb.AppendLine("${prefix}<Directory Id=""$childId"" Name=""$($child.Name)"">")
        Write-Dirs $childRel ($indent + 2)
        [void]$sb.AppendLine("${prefix}</Directory>")
    }
}

Write-Dirs "" 6
[void]$sb.AppendLine('    </DirectoryRef>')

# ---- ComponentGroup with one Component per file ----
[void]$sb.AppendLine('    <ComponentGroup Id="HarvestedAppFiles">')

foreach ($file in $allFiles) {
    $relPath   = $file.FullName.Substring($SourceDir.Length).TrimStart('\')
    $relDir    = Split-Path $relPath -Parent
    $guid      = Get-DeterministicGuid $relPath
    $compId    = Safe-Id $relPath
    $dirId     = Dir-Id $relDir
    # Path from Installer/ back to the staged source
    $srcPath   = "..\deployment_package\InstallerSrc\Main\$relPath"

    [void]$sb.AppendLine("      <Component Id=""$compId"" Guid=""{$guid}"" Directory=""$dirId"">")
    [void]$sb.AppendLine("        <File Source=""$srcPath"" KeyPath=""yes"" />")
    [void]$sb.AppendLine("      </Component>")
}

[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')

$OutputDir = Split-Path $OutputFile -Parent
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }

Set-Content -Path $OutputFile -Value $sb.ToString() -Encoding UTF8
Write-Host "Harvested $($allFiles.Count) files → $OutputFile"
