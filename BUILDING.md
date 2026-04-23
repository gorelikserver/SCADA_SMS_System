# Building a Release

```powershell
.\build-release.ps1 -Version 2.3.0
```

Produces `SCADA_SMS_v2.3.0_Setup.msi` in the repo root.

## What it does

1. Bumps `ProductVersion` in `Installer/Variables.wxi`
2. `dotnet publish` — self-contained win-x64
3. Stages files for WiX (separates `SCADASMSSystem.Web.exe` and `appsettings.json`)
4. Runs `Installer/Scripts/Generate-HarvestedFiles.ps1`
5. `dotnet build Installer/SCADASMSInstaller.wixproj`
6. Copies MSI to repo root

## Prerequisites

.NET 9 SDK and WiX v4 (one-time setup):

```powershell
dotnet tool install --global wix
wix extension add WixToolset.UI.wixext WixToolset.Util.wixext WixToolset.Firewall.wixext
```
