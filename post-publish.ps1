$ErrorActionPreference = 'Stop'

$publishDir = Join-Path $PSScriptRoot 'ByProxy'
$dllPath = Join-Path $publishDir 'ByProxy.dll'
$runtimesDir = Join-Path $publishDir 'runtimes'

$version = [Version](Get-Item -LiteralPath $dllPath).VersionInfo.FileVersion
$tagFull = '{0}.{1}.{2}' -f $version.Major, $version.Minor, $version.Build
$zipPath = Join-Path $PSScriptRoot "ByProxy-v$tagFull.zip"

$runtimesToKeep = @(
    'win-x64', 'win-arm64',
    'osx-x64', 'osx-arm64',
    'linux-x64', 'linux-arm64',
    'linux-musl-x64', 'linux-musl-arm64'
)

$runtimes = Get-ChildItem -LiteralPath $runtimesDir -Directory
foreach ($runtime in $runtimes) {
    if ($runtime.Name -notin $runtimesToKeep) {
        Write-Host "Removing runtime: $($runtime.Name)"
        Remove-Item -LiteralPath $runtime.FullName -Recurse -Force
    }
}

Compress-Archive -Path $publishDir -DestinationPath $zipPath 
Write-Host "Created: $zipPath"