param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Build\PlayerPackage'),
    [string]$PackageName = 'AquacultureFishing'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = Split-Path -Parent $PSScriptRoot
$assemblies = Join-Path $root '1.6\Assemblies'
$sourceAssembly = Join-Path $assemblies 'AquacultureFishing.dll'
if (-not (Test-Path -LiteralPath $sourceAssembly -PathType Leaf)) { throw "Production assembly missing: $sourceAssembly" }

$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
$stage = Join-Path $resolvedOutput "$PackageName-stage"
$zip = Join-Path $resolvedOutput "$PackageName.zip"
$manifestPath = Join-Path $resolvedOutput "$PackageName.manifest.json"
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
New-Item -ItemType Directory -Force -Path $resolvedOutput | Out-Null

function Copy-IfPresent {
    param([string]$RelativePath)
    $source = Join-Path $root $RelativePath
    if (Test-Path -LiteralPath $source) {
        $destination = Join-Path $stage $RelativePath
        $parent = Split-Path -Parent $destination
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Recurse -Force
    }
}

Copy-IfPresent 'About\About.xml'
Copy-IfPresent 'LoadFolders.xml'
Copy-IfPresent '1.6\Defs'
Copy-IfPresent '1.6\Languages'
Copy-IfPresent '1.6\Textures'
$stageAssemblies = Join-Path $stage '1.6\Assemblies'
New-Item -ItemType Directory -Force -Path $stageAssemblies | Out-Null
foreach ($name in @('AquacultureFishing.dll', 'DeferredReality.Aquaculture.dll', 'DeferredReality.Aquaculture.manifest.json')) {
    $source = Join-Path $assemblies $name
    if (Test-Path -LiteralPath $source -PathType Leaf) { Copy-Item -LiteralPath $source -Destination (Join-Path $stageAssemblies $name) -Force }
}

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$entries = [IO.Compression.ZipFile]::OpenRead($zip).Entries.FullName
$forbidden = @($entries | Where-Object { $_ -match '(^|/)(DevTools|Source|bin|obj|Build|Runtime)(/|$)|\.pdb$|\.exe$|BridgeAdapter|InGameTest' })
if ($forbidden.Count -gt 0) { throw "Forbidden package entries: $($forbidden -join ', ')" }

$assemblyRecords = @()
foreach ($file in Get-ChildItem -LiteralPath $stageAssemblies -File) {
    $assemblyRecords += [ordered]@{ name = $file.Name; bytes = $file.Length; sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
}
$statusLines = @(& git -C $root status --porcelain --untracked-files=all)
$sourceDirty = $statusLines.Count -gt 0
$manifest = [ordered]@{
    schemaVersion = 1
    packageId = 'lan.aquaculture.fishing'
    sourceCommit = (& git -C $root rev-parse HEAD).Trim()
    sourceDirty = $sourceDirty
    rimWorldTarget = '1.6'
    frameworkPackages = @('lan.insightcanvas', 'lan.deferredreality.framework', 'lan.knowledgeframework')
    assemblyFiles = $assemblyRecords
    packageFile = $zip
    packageSha256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    knownReleaseBlockers = @('Dedicated production art is incomplete', 'Manual gameplay/save-load/compatibility/performance gates remain')
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Remove-Item -LiteralPath $stage -Recurse -Force
Write-Output "Package: $zip"
Write-Output "Manifest: $manifestPath"
Write-Output "Package SHA256: $($manifest.packageSha256)"
