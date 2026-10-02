<#
.SYNOPSIS
    Publish Startup Profiles self-contained and compile the per-user Inno Setup installer.

.DESCRIPTION
    Adapted from CodePrint's templates/dotnet/installer/build-installer.ps1. The version comes from
    VersionPrefix in Directory.Build.props unless -Version is given. Output:
    installer/dist/StartupProfiles-Setup-<version>.exe and, unless -NoChecksum, its .sha256 next to it
    ("<hash>  <file name>", the format the in-app updater checks). The release workflow passes
    -NoChecksum, signs the setup, and only then writes the checksum, so the hash matches the signed file.
    installer/publish and installer/dist are ignored by Git.

.EXAMPLE
    ./installer/build-installer.ps1 -Run
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$IsccPath,
    # An existing publish folder (the release workflow's signed build). Without it this script publishes.
    [string]$PublishDir,
    [switch]$NoChecksum,
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$installerRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $repositoryRoot 'src\StartupProfiles.App\StartupProfiles.App.csproj'

if (-not $Version) {
    [xml]$props = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props')
    $Version = @($props.Project.PropertyGroup.VersionPrefix | Where-Object { $_ })[0]
    if (-not $Version) { throw 'Directory.Build.props has no <VersionPrefix>. Pass -Version explicitly.' }
}
if ($Version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
    throw "Version is not a supported semantic version: $Version"
}
$versionInfoVersion = ([regex]::Match($Version, '^\d+\.\d+\.\d+')).Value + '.0'

if (-not $IsccPath) {
    $IsccPath = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
    throw 'Inno Setup 6 ISCC.exe was not found. Install it (winget install JRSoftware.InnoSetup) or pass -IsccPath.'
}

$dist = Join-Path $installerRoot 'dist'
[System.IO.Directory]::CreateDirectory($dist) | Out-Null

if ($PublishDir) {
    $publish = [System.IO.Path]::GetFullPath($PublishDir)
    if (-not (Test-Path -LiteralPath (Join-Path $publish 'StartupProfiles.exe'))) { throw "No StartupProfiles.exe in $publish" }
} else {
    $publish = Join-Path $installerRoot 'publish'
    if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
    [System.IO.Directory]::CreateDirectory($publish) | Out-Null

    Write-Host "Publishing Startup Profiles $Version ($Configuration, $Runtime)..." -ForegroundColor Cyan
    & dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
        -p:PublishSingleFile=false -p:Version=$Version -o $publish --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
}

& $IsccPath "/DMyAppVersion=$Version" "/DMyVersionInfoVersion=$versionInfoVersion" `
    "/DPublishDir=$publish" (Join-Path $installerRoot 'StartupProfiles.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

# The setup holds the files now. Drop a publish folder this script made: CodePrint's validator scans it.
if (-not $PublishDir) { Remove-Item -LiteralPath $publish -Recurse -Force }

$setup = Join-Path $dist "StartupProfiles-Setup-$Version.exe"
if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw "Expected installer was not produced: $setup" }

if (-not $NoChecksum) {
    $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    [System.IO.File]::WriteAllText("$setup.sha256", "$hash  $([System.IO.Path]::GetFileName($setup))`n",
        [System.Text.UTF8Encoding]::new($false))
}

if ((Get-AuthenticodeSignature -LiteralPath $setup).Status -ne 'Valid') {
    Write-Warning 'The installer is not Authenticode-signed. See docs/release-signing.md before publishing it.'
}
Write-Host "Built $setup" -ForegroundColor Green
if ($Run) { Start-Process -FilePath $setup }
