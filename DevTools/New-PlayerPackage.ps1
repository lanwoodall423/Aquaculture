param(
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Build\PlayerPackage'),
    [string]$PackageName = 'AquacultureFishing'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = Split-Path -Parent $PSScriptRoot
$assemblies = Join-Path $root '1.7\Assemblies'
$sourceAssembly = Join-Path $assemblies 'AquacultureFishing.dll'
if (-not (Test-Path -LiteralPath $sourceAssembly -PathType Leaf)) {
    throw "RimWorld 1.7 production assembly missing (build the net10.0 core first): $sourceAssembly"
}

# A tracked 1.6 DLL is never usable as a 1.7 runtime artifact.
$coreFiles = @(Get-ChildItem -LiteralPath $assemblies -File)
if ($coreFiles.Count -ne 1 -or $coreFiles[0].Name -ne 'AquacultureFishing.dll') {
    throw 'Unconditional 1.7 Assemblies must contain only AquacultureFishing.dll.'
}
[xml]$aboutContract = Get-Content -LiteralPath (Join-Path $root 'About\About.xml') -Raw
$versions = @($aboutContract.SelectNodes('/ModMetaData/supportedVersions/li'))
if ($versions.Count -ne 1 -or $versions[0].InnerText.Trim() -ne '1.7') {
    throw 'About.xml must advertise RimWorld 1.7 exclusively.'
}
[xml]$folderContract = Get-Content -LiteralPath (Join-Path $root 'LoadFolders.xml') -Raw
$loadVersions = @($folderContract.SelectNodes('/loadFolders/*'))
$coreFolder = $folderContract.SelectSingleNode("/loadFolders/v1.7/li[not(@*) and text()='1.7']")
$conditionalDrf = $folderContract.SelectSingleNode("/loadFolders/v1.7/li[@IfModActive='lan.deferredreality.framework' and text()='1.7/DeferredReality']")
$folderEntries = @($folderContract.SelectNodes('/loadFolders/v1.7/li'))
if ($loadVersions.Count -ne 1 -or $loadVersions[0].Name -ne 'v1.7' -or
    $folderEntries.Count -ne 2 -or $null -eq $coreFolder -or $null -eq $conditionalDrf -or
    $conditionalDrf.Attributes.Count -ne 1) {
    throw 'LoadFolders.xml requires exactly the 1.7 core and IfModActive-gated DRF folders.'
}

# The tracked 1.6 files are *source seeds* only. The Release build must
# recreate identical 1.7 XML/PNG assets before packaging them.
$assetGroups = @(
    @{ Seed = '1.6\Defs'; Generated = '1.7\Defs'; Extension = '.xml' },
    @{ Seed = '1.6\Languages'; Generated = '1.7\Languages'; Extension = '.xml' },
    @{ Seed = '1.6\Textures'; Generated = '1.7\Textures'; Extension = '.png' }
)
foreach ($group in $assetGroups) {
    $seedRoot = Join-Path $root $group.Seed
    $generatedRoot = Join-Path $root $group.Generated
    if (-not (Test-Path -LiteralPath $seedRoot -PathType Container) -or
        -not (Test-Path -LiteralPath $generatedRoot -PathType Container)) {
        throw "RimWorld 1.7 content staging is incomplete: $($group.Generated)"
    }
    $seeds = @(Get-ChildItem -LiteralPath $seedRoot -Recurse -File)
    $generated = @(Get-ChildItem -LiteralPath $generatedRoot -Recurse -File)
    if ($seeds.Count -eq 0 -or $seeds.Count -ne $generated.Count) {
        throw "Staged 1.7 assets are missing or stale: $($group.Generated)"
    }
    foreach ($seed in $seeds) {
        if ($seed.Extension -ne $group.Extension) {
            throw "Unexpected source asset type in $($group.Seed): $($seed.Name)"
        }
        $relative = $seed.FullName.Substring($seedRoot.Length).TrimStart([char]'\', [char]'/')
        $outputFile = Join-Path $generatedRoot $relative
        if (-not (Test-Path -LiteralPath $outputFile -PathType Leaf) -or
            (Get-FileHash -LiteralPath $seed.FullName -Algorithm SHA256).Hash -ne
            (Get-FileHash -LiteralPath $outputFile -Algorithm SHA256).Hash) {
            throw "Staged 1.7 content does not match the source seed: $relative"
        }
    }
}

$providerRelative = '1.7\DeferredReality\Assemblies'
$providerSource = Join-Path $root $providerRelative
$providerAssembly = Join-Path $providerSource 'DeferredReality.Aquaculture.dll'
$providerManifest = Join-Path $providerSource 'DeferredReality.Aquaculture.manifest.json'
$hasProviderAssembly = Test-Path -LiteralPath $providerAssembly -PathType Leaf
$hasProviderManifest = Test-Path -LiteralPath $providerManifest -PathType Leaf
if ($hasProviderAssembly -ne $hasProviderManifest) {
    throw 'Optional Deferred Reality provider must have both DLL and manifest, or neither.'
}
if (Test-Path -LiteralPath $providerSource -PathType Container) {
    $providerFiles = @(Get-ChildItem -LiteralPath $providerSource -File)
    $unknownProviderFiles = @($providerFiles | Where-Object {
        @('DeferredReality.Aquaculture.dll', 'DeferredReality.Aquaculture.manifest.json') -notcontains $_.Name
    })
    if ($unknownProviderFiles.Count -gt 0) {
        throw 'Unexpected file in the conditional 1.7 Deferred Reality provider folder.'
    }
}
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
}

if ($PackageName -notmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,63}$') {
    throw 'PackageName must be a simple filename without directory components.'
}
$resolvedOutput = [IO.Path]::GetFullPath($OutputDirectory)
$stage = Join-Path $resolvedOutput "$PackageName-stage"
$zip = Join-Path $resolvedOutput "$PackageName.zip"
$manifestPath = Join-Path $resolvedOutput "$PackageName.manifest.json"
if ([IO.Path]::GetFullPath($stage).TrimEnd('\') -eq [IO.Path]::GetFullPath($root).TrimEnd('\')) {
    throw 'The player package stage cannot be the repository root.'
}
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

function Copy-Required {
    param([string]$RelativePath)
    $source = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $source)) { throw "Required player package source missing: $RelativePath" }
    $destination = Join-Path $stage $RelativePath
    $parent = Split-Path -Parent $destination
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Recurse -Force
}

Copy-Required 'About\About.xml'
Copy-Required 'LICENSE'
Copy-Required 'LoadFolders.xml'
Copy-Required '1.7\Defs'
Copy-Required '1.7\Languages'
Copy-Required '1.7\Textures'
$stageAssemblies = Join-Path $stage '1.7\Assemblies'
New-Item -ItemType Directory -Force -Path $stageAssemblies | Out-Null
# Only the core DLL may reside in the unconditionally loaded Assemblies folder.
Copy-Item -LiteralPath $sourceAssembly -Destination (Join-Path $stageAssemblies 'AquacultureFishing.dll') -Force

$stageProviderAssemblies = Join-Path $stage $providerRelative
New-Item -ItemType Directory -Force -Path $stageProviderAssemblies | Out-Null
if ($hasProviderAssembly) {
    Copy-Item -LiteralPath $providerAssembly -Destination (Join-Path $stageProviderAssemblies 'DeferredReality.Aquaculture.dll') -Force
    Copy-Item -LiteralPath $providerManifest -Destination (Join-Path $stageProviderAssemblies 'DeferredReality.Aquaculture.manifest.json') -Force
}

if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try { $entries = @($archive.Entries | ForEach-Object FullName) }
finally { $archive.Dispose() }
$forbidden = @($entries | Where-Object {
    $_ -match '(^|/)1[.]6(/|$)|(^|/)(DevTools|Source|bin|obj|Build|Runtime)(/|$)|\.pdb$|\.exe$|BridgeAdapter|InGameTest'
})
if ($forbidden.Count -gt 0) { throw "Forbidden package entries: $($forbidden -join ', ')" }
# Verify the final ZIP, not merely the staging tree, before publishing its manifest.
& (Join-Path $PSScriptRoot 'Test-PlayerPackage.ps1') -PackagePath $zip | Out-Null

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
$manifest = [ordered]@{
    schemaVersion = 1
    packageId = 'lan.aquaculture.fishing'
    sourceCommit = (& git -C $root rev-parse HEAD).Trim()
    sourceDirty = $statusLines.Count -gt 0
    rimWorldTarget = '1.7'
    frameworkPackages = @('lan.insightcanvas', 'lan.deferredreality.framework', 'lan.knowledgeframework')
    assemblyFiles = $assemblyRecords
    packageFile = $zip
    packageSha256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    license = 'GPL-3.0-or-later'
    copyright = 'Copyright (C) 2026 lanwoodall423'
    knownReleaseBlockers = @('Actual RimWorld 1.7 build/load/gameplay/save and dependency gates are NOT RUN')
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
Remove-Item -LiteralPath $stage -Recurse -Force
Write-Output "Package: $zip"
Write-Output "Manifest: $manifestPath"
Write-Output "Package SHA256: $($manifest.packageSha256)"
