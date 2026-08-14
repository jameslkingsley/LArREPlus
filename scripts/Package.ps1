[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$StationeersPath
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'LarrePlus.csproj'
$project = [xml](Get-Content -LiteralPath $projectFile -Raw -Encoding UTF8)
$version = [string]$project.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Could not read Version from LarrePlus.csproj.'
}

$buildArguments = @('build', $projectFile, '--configuration', $Configuration)
if (-not [string]::IsNullOrWhiteSpace($StationeersPath)) {
    $buildArguments += "-p:StationeersPath=$StationeersPath"
}

& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$pluginPath = Join-Path $projectRoot 'LarrePlus.dll'
if (-not (Test-Path -LiteralPath $pluginPath -PathType Leaf)) {
    throw "Build succeeded but $pluginPath was not created."
}

$artifactsDirectory = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactsDirectory -Force | Out-Null
$archivePath = Join-Path $artifactsDirectory "LarrePlus-$version.zip"

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ("LarrePlus-package-" + [guid]::NewGuid().ToString('N'))
$payloadRoot = Join-Path $temporaryRoot 'LarrePlus'
New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null

try {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'About') -Destination $payloadRoot -Recurse
    Copy-Item -LiteralPath $pluginPath -Destination $payloadRoot
    Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $payloadRoot
    Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $payloadRoot
    Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination $payloadRoot

    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath
    }

    Compress-Archive -LiteralPath $payloadRoot -DestinationPath $archivePath -CompressionLevel Optimal
}
finally {
    $resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
    $resolvedSystemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolvedTemporaryRoot.StartsWith($resolvedSystemTemp, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTemporaryRoot).StartsWith('LarrePlus-package-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Created $archivePath"
