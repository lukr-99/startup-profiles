#Requires -Version 5.1
<#
.SYNOPSIS
    Uninstall Windows Startup Profiles for the current user.

.DESCRIPTION
    Stops any running instance, removes the login registration and the startupprofiles:// protocol,
    deletes the installed program, the Start Menu shortcut, and the installed agent skill. With
    -PurgeData it also deletes %APPDATA%\StartupProfiles (profiles, config, history).

    Registry keys are removed directly (rather than via the app) so uninstall works even if the
    executable is already gone.

.PARAMETER PurgeData
    Also delete %APPDATA%\StartupProfiles (your profiles, config, and history).

.EXAMPLE
    .\install\uninstall.ps1
.EXAMPLE
    .\install\uninstall.ps1 -PurgeData
#>
[CmdletBinding()]
param(
    [switch] $PurgeData
)

$ErrorActionPreference = 'Stop'

$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\StartupProfiles'
$Shortcut   = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Startup Profiles.lnk'
$SkillDir   = Join-Path $env:USERPROFILE '.claude\skills\startup-profiles'
$RunKey     = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$ProtocolKey = 'HKCU:\Software\Classes\startupprofiles'

function Write-Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }

Write-Host "Uninstalling Windows Startup Profiles..." -ForegroundColor Green

Get-Process -Name 'StartupProfiles' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Step "Stopping running instance (pid $($_.Id))"
    $_ | Stop-Process -Force
}
Start-Sleep -Milliseconds 400

Write-Step "Removing login registration"
if (Get-ItemProperty -Path $RunKey -Name 'StartupProfiles' -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $RunKey -Name 'StartupProfiles'
}

Write-Step "Removing startupprofiles:// protocol"
if (Test-Path $ProtocolKey) { Remove-Item -Path $ProtocolKey -Recurse -Force }

Write-Step "Removing Start Menu shortcut"
if (Test-Path $Shortcut) { Remove-Item -Path $Shortcut -Force }

Write-Step "Removing agent skill"
if (Test-Path $SkillDir) { Remove-Item -Path $SkillDir -Recurse -Force }

Write-Step "Removing installed program"
if (Test-Path $InstallDir) { Remove-Item -Path $InstallDir -Recurse -Force }

if ($PurgeData) {
    $dataDir = Join-Path $env:APPDATA 'StartupProfiles'
    Write-Step "Purging data at $dataDir"
    if (Test-Path $dataDir) { Remove-Item -Path $dataDir -Recurse -Force }
}

Write-Host ""
Write-Host "Uninstalled." -ForegroundColor Green
if (-not $PurgeData) { Write-Host "Your profiles remain in %APPDATA%\StartupProfiles (use -PurgeData to remove them)." }
