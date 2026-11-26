<#
.SYNOPSIS
    Publishes KeyboardMixer (Release, x64, self-contained, trimmed) and builds the installer.

.DESCRIPTION
    Output: artifacts\KeyboardMixer-Setup-<version>.exe
    Requires Inno Setup 6:  winget install JRSoftware.InnoSetup
#>
[CmdletBinding()]
param(
    # Path to ISCC.exe if Inno Setup isn't in one of the usual places.
    [string]$Iscc
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$publishDir = Join-Path $root 'artifacts\publish'

if (-not $Iscc) {
    $Iscc = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Iscc -or -not (Test-Path $Iscc)) {
    throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup"
}

Write-Host "Publishing..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $root 'src\KeyboardMixer') -c Release -p:Platform=x64 -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

# Version from the built exe, e.g. "2.0.0" (drops the "+commit" suffix).
$version = (Get-Item (Join-Path $publishDir 'KeyboardMixer.exe')).VersionInfo.ProductVersion -replace '\+.*$', ''

Write-Host "Building installer for $version..." -ForegroundColor Cyan
& $Iscc /Q "/DAppVersion=$version" "/DPublishDir=$publishDir" (Join-Path $root 'installer\KeyboardMixer.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$setup = Join-Path $root "artifacts\KeyboardMixer-Setup-$version.exe"
$appSize = (Get-ChildItem $publishDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Done: {0} ({1:N1} MB download, {2:N0} MB installed)" -f $setup, ((Get-Item $setup).Length / 1MB), $appSize) -ForegroundColor Green
