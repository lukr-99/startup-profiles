#Requires -Version 5.1
<#
.SYNOPSIS
    Uninstall Windows Startup Profiles (placeholder).

.DESCRIPTION
    Skeleton uninstaller. Will remove the installed program, Start Menu shortcut, login
    registration, and the startupprofiles:// protocol. With -PurgeData it will also delete
    %APPDATA%\StartupProfiles. Mirrors treeline/install/uninstall.ps1.

.NOTES
    Not implemented yet - the solution is a skeleton.
#>
[CmdletBinding()]
param(
    [switch] $PurgeData
)

Write-Host "Windows Startup Profiles is still a skeleton - nothing to uninstall yet." -ForegroundColor Yellow
exit 1
