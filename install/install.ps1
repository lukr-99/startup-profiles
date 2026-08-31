#Requires -Version 5.1
<#
.SYNOPSIS
    Install Windows Startup Profiles (placeholder).

.DESCRIPTION
    Skeleton installer. Once the app builds, this will publish StartupProfiles, install it to
    %LOCALAPPDATA%\Programs\StartupProfiles, add a Start Menu shortcut, register the launcher
    to run at login, register the startupprofiles:// protocol, and install the Claude agent
    skill. Mirrors treeline/install/install.ps1.

.NOTES
    Not implemented yet - the solution is a skeleton.
#>
[CmdletBinding()]
param(
    [int]    $Port = 8790,
    [switch] $FrameworkDependent,
    [switch] $NoStartup
)

Write-Host "Windows Startup Profiles is still a skeleton - nothing to install yet." -ForegroundColor Yellow
Write-Host "Build with: dotnet build StartupProfiles.slnx"
exit 1
