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
Copy-IfPresent 'LICENSE'
Copy-IfPresent 'LoadFolders.xml'
Copy-IfPresent '1.6\Defs'
Copy-IfPresent '1.6\Languages'
Copy-IfPresent '1.6\Textures'
$stageAssemblies = Join-Path $stage '1.6\Assemblies'
New-Item -ItemType Directory -Force -Path $stageAssemblies | Out-Null
# Only the core assembly may reside in the unconditionally loaded Assemblies folder.
Copy-Item -LiteralPath $sourceAssembly -Destination (Join-Path $stageAssemblies 'AquacultureFishing.dll') -Force

$providerRelative = '1.6\DeferredReality\Assemblies'
$providerSource = Join-Path $root $providerRelative
$providerAssembly = Join-Path $providerSource 'DeferredReality.Aquaculture.dll'
$providerManifest = Join-Path $providerSource 'DeferredReality.Aquaculture.manifest.json'
$hasProviderAssembly = Test-Path -LiteralPath $providerAssembly -PathType Leaf
$hasProviderManifest = Test-Path -LiteralPath $providerManifest -PathType Leaf
if ($hasProviderAssembly -ne $hasProviderManifest) {
    throw 'Optional Deferred Reality provider must have both DLL and manifest, or neither.'
}
$stageProviderAssemblies = Join-Path $stage $providerRelative
# Keep the conditionally referenced load-folder present even when no provider was built.
New-Item -ItemType Directory -Force -Path $stageProviderAssemblies | Out-Null
[xml]$folderContract = Get-Content -LiteralPath (Join-Path $root 'LoadFolders.xml') -Raw
$conditionalDrf = $folderContract.SelectSingleNode("/loadFolders/v1.6/li[@IfModActive='lan.deferredreality.framework' and text()='1.6/DeferredReality']")
if ($null -eq $conditionalDrf) { throw 'DRF assembly folder must be IfModActive-gated in LoadFolders.xml.' }
if ($hasProviderAssembly) {
    $providerMetadata = Get-Content -LiteralPath $providerManifest -Raw | ConvertFrom-Json
    $providerHash = (Get-FileHash -LiteralPath $providerAssembly -Algorithm SHA256).Hash
    if ($providerMetadata.providerId -ne 'lan.aquaculture.natural-water' -or
        $providerMetadata.assemblyName -ne 'DeferredReality.Aquaculture' -or
        $providerMetadata.sha256 -ne $providerHash -or
        $providerMetadata.semanticApiVersion -lt 2 -or
        $providerMetadata.providerSchemaVersion -lt 2) {
        throw 'Optional Deferred Reality provider manifest identity, schema or SHA256 mismatch.'
    }
    Copy-Item -LiteralPath $providerAssembly -Destination (Join-Path $stageProviderAssemblies 'DeferredReality.Aquaculture.dll') -Force
    Copy-Item -LiteralPath $providerManifest -Destination (Join-Path $stageProviderAssemblies 'DeferredReality.Aquaculture.manifest.json') -Force
}

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$entries = [IO.Compression.ZipFile]::OpenRead($zip).Entries.FullName
$forbidden = @($entries | Where-Object { $_ -match '(^|/)(DevTools|Source|bin|obj|Build|Runtime)(/|$)|\.pdb$|\.exe$|BridgeAdapter|InGameTest' })
if ($forbidden.Count -gt 0) { throw "Forbidden package entries: $($forbidden -join ', ')" }

$assemblyRecords = @()
$assemblyDirs = @($stageAssemblies)
if ($hasProviderAssembly) { $assemblyDirs += $stageProviderAssemblies }
foreach ($assemblyDirectory in $assemblyDirs) {
    foreach ($file in Get-ChildItem -LiteralPath $assemblyDirectory -File) {
        $relativePath = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        $assemblyRecords += [ordered]@{
            name = $file.Name
            relativePath = $relativePath
            bytes = $file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
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
    license = 'GPL-3.0-or-later'
    copyright = 'Copyright (C) 2026 lanwoodall423'
    knownReleaseBlockers = @('Manual gameplay/save-load/compatibility/performance gates remain')
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Remove-Item -LiteralPath $stage -Recurse -Force
Write-Output "Package: $zip"
Write-Output "Manifest: $manifestPath"
Write-Output "Package SHA256: $($manifest.packageSha256)"
