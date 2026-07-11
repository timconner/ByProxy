param(
    [switch]$Force,     # Bypass Confirmation
    [switch]$NoPush,     # Don't Push to Docker Hub
    [switch]$NoLatest,     # Don't Build/Push latest tag
    [switch]$LatestOnly     # Only build latest tag
)

$dllPath = ".\ByProxy\ByProxy.dll"
$repo = "timconner/byproxy"

$version = [Version](Get-Item $dllPath).VersionInfo.FileVersion
$tagMajorMinor = "{0}.{1}" -f $version.Major, $version.Minor
$tagFull = "{0}.{1}.{2}" -f $version.Major, $version.Minor, $version.Build

if ($NoLatest -and $LatestOnly) {
    Write-Error "Conflicting options: -NoLatest and -LatestOnly cannot be used together."
    exit 1
}

if ($LatestOnly) {
    $tags = @(
        "$repo`:latest"
    )
} else {
    $tags = @(
        "$repo`:$tagMajorMinor"
        "$repo`:$tagFull"
    )

    if (-not $NoLatest) {
        $tags += "$repo`:latest"
    }
}

Write-Host ("`nVersion: {0}.{1}.{2}" -f $version.Major, $version.Minor, $version.Build)
Write-Host "The following tags will be built:`n"

$tags | ForEach-Object { Write-Host "    $_" }

if ($NoPush) {
    Write-Host "`nPush will be skipped due to -NoPush flag."
} else {
    Write-Host "`nThese tags will also be pushed."
}

if (-not $Force) {
    $response = Read-Host "`nProceed? (Y/N)"
    if ($response -notin @("Y", "y")) {
        Write-Host "Aborted."
        exit 1
    }
}

Write-Host

$buildArgs = @()
foreach ($tag in $tags) {
    $buildArgs += "-t"
    $buildArgs += $tag
}
$buildArgs += "."

docker build @buildArgs

if (-not $NoPush) {
    foreach ($tag in $tags) {
        docker push $tag
    }
}
