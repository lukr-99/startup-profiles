#Requires -Version 5.1
<#
.SYNOPSIS
    Install Windows Startup Profiles for the current user (no elevation required).

.DESCRIPTION
    Publishes StartupProfiles, installs it to %LOCALAPPDATA%\Programs\StartupProfiles, adds a Start
    Menu shortcut, registers the launcher to run at login, registers the startupprofiles:// protocol,
    and installs the Claude agent skill to %USERPROFILE%\.claude\skills. All state is per-user.

    Login and protocol registration are performed by the app itself (StartupProfiles.exe
    --register-login / --register-protocol) so the registry formats stay owned by the app's seams.

.PARAMETER Port
    Loopback API port to persist in config.json (default 8790). Only written when non-default.

.PARAMETER FrameworkDependent
    Publish a smaller framework-dependent build (requires the .NET 10 Desktop Runtime installed).
    The default is a self-contained build that bundles the runtime.

.PARAMETER NoStartup
    Do not register the launcher to run at login.

.PARAMETER NoProtocol
    Do not register the startupprofiles:// protocol handler.

.PARAMETER NoSkill
    Do not install the Claude agent skill.

.EXAMPLE
    .\install\install.ps1
.EXAMPLE
    .\install\install.ps1 -FrameworkDependent -NoStartup
#>
[CmdletBinding()]
param(
    [int]    $Port = 8790,
    [switch] $FrameworkDependent,
    [switch] $NoStartup,
    [switch] $NoProtocol,
    [switch] $NoSkill
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$AppProject = Join-Path $RepoRoot 'src\StartupProfiles.App\StartupProfiles.App.csproj'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\StartupProfiles'
$ExePath    = Join-Path $InstallDir 'StartupProfiles.exe'

function Write-Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }

function Stop-RunningInstance {
    Get-Process -Name 'StartupProfiles' -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Step "Stopping running instance (pid $($_.Id))"
        $_ | Stop-Process -Force
    }
    Start-Sleep -Milliseconds 400
}

function Publish-App {
    $selfContained = (-not $FrameworkDependent).ToString().ToLower()
    Write-Step "Publishing (self-contained=$selfContained) to $InstallDir"

    if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir }
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

    & dotnet publish $AppProject -c Release -r win-x64 --self-contained $selfContained `
        -p:PublishSingleFile=false -o $InstallDir --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path $ExePath)) { throw "Publish did not produce $ExePath." }
}

function Add-StartMenuShortcut {
    $startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $lnk = Join-Path $startMenu 'Startup Profiles.lnk'
    Write-Step "Creating Start Menu shortcut"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($lnk)
    $shortcut.TargetPath = $ExePath
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.Description = 'Windows Startup Profiles'
    $shortcut.Save()
}

function Set-ConfigPort {
    if ($Port -eq 8790) { return }
    Write-Step "Persisting API port $Port"
    $dir = Join-Path $env:APPDATA 'StartupProfiles'
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $path = Join-Path $dir 'config.json'
    $config = if (Test-Path $path) { Get-Content $path -Raw | ConvertFrom-Json } else { $null }
    if ($null -eq $config) { $config = [pscustomobject]@{} }
    $config | Add-Member -NotePropertyName 'port' -NotePropertyValue "$Port" -Force
    $config | ConvertTo-Json | Set-Content -Path $path -Encoding utf8
}

function Invoke-Maintenance($argument) {
    $proc = Start-Process -FilePath $ExePath -ArgumentList $argument -Wait -PassThru
    if ($proc.ExitCode -ne 0) { throw "$ExePath $argument exited with $($proc.ExitCode)." }
}

function Install-Skill {
    $source = Join-Path $RepoRoot 'skills\startup-profiles'
    if (-not (Test-Path $source)) { Write-Warning "Agent skill not found at $source; skipping."; return }
    $dest = Join-Path $env:USERPROFILE '.claude\skills\startup-profiles'
    Write-Step "Installing agent skill to $dest"
    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item -Path (Join-Path $source '*') -Destination $dest -Recurse -Force
}

Write-Host "Installing Windows Startup Profiles..." -ForegroundColor Green
Stop-RunningInstance
Publish-App
Add-StartMenuShortcut
Set-ConfigPort

if (-not $NoStartup) { Write-Step "Registering launcher at login"; Invoke-Maintenance '--register-login' }
if (-not $NoProtocol) { Write-Step "Registering startupprofiles:// protocol"; Invoke-Maintenance '--register-protocol' }
if (-not $NoSkill) { Install-Skill }

Write-Host ""
Write-Host "Installed to $InstallDir" -ForegroundColor Green
Write-Host "Launch it now:  `"$ExePath`""
if (-not $NoStartup) { Write-Host "It will also appear at your next login." }
