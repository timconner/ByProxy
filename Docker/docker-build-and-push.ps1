param(
    [switch]$Force,     # Bypass Confirmation
    [switch]$NoPush     # Don't Push to Docker Hub
)

$dllPath = ".\ByProxy\ByProxy.dll"
$repo = "timconner/byproxy"

$version = [Version](Get-Item $dllPath).VersionInfo.FileVersion
$tagMajorMinor = "{0}.{1}" -f $version.Major, $version.Minor
$tagFull = "{0}.{1}.{2}" -f $version.Major, $version.Minor, $version.Build

$tags = @(
    "$repo`:latest"
    "$repo`:$tagMajorMinor"
    "$repo`:$tagFull"
)

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
