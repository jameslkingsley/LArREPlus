[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

$requiredFiles = @(
    'About/About.xml',
    'About/Preview.png',
    'About/thumb.png',
    'LarrePlus.csproj',
    'README.md',
    'LICENSE',
    'CHANGELOG.md',
    'CONTRIBUTING.md',
    'THIRD_PARTY_NOTICES.md',
    'src/LarrePlusMod.cs',
    'src/AimeeCargoArmCompatibility.cs',
    'src/ArmEnhancements.cs'
)

foreach ($relativePath in $requiredFiles) {
    $path = Join-Path $projectRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing required file: $relativePath"
    }
}

$about = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'About/About.xml') -Raw -Encoding UTF8)
$project = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'LarrePlus.csproj') -Raw -Encoding UTF8)
$source = Get-Content -LiteralPath (Join-Path $projectRoot 'src/LarrePlusMod.cs') -Raw -Encoding UTF8
$aimeeSource = Get-Content -LiteralPath (Join-Path $projectRoot 'src/AimeeCargoArmCompatibility.cs') -Raw -Encoding UTF8

$expectedId = 'com.james.larreplus'
$expectedAssembly = 'LarrePlus'
$expectedRepository = 'https://github.com/jameslkingsley/LArREPlus'
$version = [string]$project.Project.PropertyGroup.Version

if ([string]$about.ModMetadata.ModID -ne $expectedId) {
    throw "About.xml ModID does not equal $expectedId."
}
if ([string]$project.Project.PropertyGroup.AssemblyName -ne $expectedAssembly) {
    throw "AssemblyName does not equal $expectedAssembly."
}
if ([string]$about.ModMetadata.Version -ne $version) {
    throw 'About.xml and project versions do not match.'
}
if ([string]$project.Project.PropertyGroup.RepositoryUrl -ne $expectedRepository) {
    throw 'The project RepositoryUrl is incorrect.'
}
if ($about.OuterXml -notmatch [regex]::Escape($expectedRepository)) {
    throw 'About.xml does not contain the public repository URL.'
}
if ($source -notmatch [regex]::Escape("public const string ModId = `"$expectedId`";")) {
    throw 'LarrePlusMod.ModId does not match About.xml.'
}
if ($source -notmatch [regex]::Escape("public const string Version = `"$version`";")) {
    throw 'LarrePlusMod.Version does not match the project version.'
}
if ($source -notmatch 'Config\.Reload\(\);[\s\S]*ArmEnhancements\.ConfigureSpeed') {
    throw 'LaunchPad configuration is not reloaded before applying movement speed.'
}
if ($aimeeSource -notmatch 'WholeAimeeSlotIndex\s*=\s*50' -or
    $aimeeSource -notmatch 'OnServer\.MoveToSlot\(targetRobot, handSlot\)' -or
    $aimeeSource -notmatch 'OnServer\.MoveToWorld\(heldRobot, position, rotation\)' -or
    $aimeeSource -notmatch 'IsCargoArmHandSlot' -or
    $aimeeSource -notmatch 'HarmonyPatch\(typeof\(DraggableThing\), nameof\(DraggableThing\.CanEnter\)\)') {
    throw 'Whole-AIMeE transport is missing an expected pickup, release, or scoped slot guard.'
}

$bundledDependencies = Get-ChildItem -LiteralPath $projectRoot -Recurse -File -Filter '*.dll' |
    Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
        $_.Name -ne 'LarrePlus.dll'
    }
if ($bundledDependencies) {
    throw "Unexpected bundled dependency DLLs: $($bundledDependencies.FullName -join ', ')"
}

$mojibakeMarkers = @(
    (-join @([char]0x00e2, [char]0x20ac, [char]0x201d)),
    (-join @([char]0x00e2, [char]0x20ac, [char]0x201c)),
    (-join @([char]0x00ef, [char]0x00bb, [char]0x00bf)),
    [string][char]0xfffd
)
$textFiles = Get-ChildItem -LiteralPath $projectRoot -Recurse -File |
    Where-Object {
        $_.FullName -ne $PSCommandPath -and
        $_.FullName -notmatch '[\\/](bin|obj|artifacts)[\\/]' -and
        $_.Extension -in '.cs', '.csproj', '.md', '.ps1', '.xml', '.yml'
    }
foreach ($file in $textFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($marker in $mojibakeMarkers) {
        if ($content.Contains($marker)) {
            throw "Possible encoding corruption '$marker' in $($file.FullName)."
        }
    }
}

Write-Host "Repository metadata and LaunchPad structure are valid for version $version."
