$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$aboutPath = Join-Path $root 'About\About.xml'
$about = [xml](Get-Content -LiteralPath $aboutPath -Raw)
$metadata = $about.ModMetaData
$required = @($metadata.modDependencies.li | ForEach-Object { [string]$_.packageId })
$loadAfter = @($metadata.loadAfter.li | ForEach-Object { [string]$_ })

if ($required -contains 'VanillaExpanded.VCEFAddon') {
    throw 'Fishing Treasures must not be a mandatory mod dependency.'
}
if ($required -notcontains 'VanillaExpanded.VCEF') {
    throw 'Vanilla Fishing Expanded must remain a mandatory dependency.'
}
if ($loadAfter -notcontains 'VanillaExpanded.VCEFAddon') {
    throw 'Fishing Treasures must remain an optional load-order hint.'
}

$forbidden = @('VanillaExpanded.VCEFAddon', 'FishingTreasures', 'Fishing Treasures')
$scanFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $root 'Source') -Filter '*.cs' -File
    Get-ChildItem -LiteralPath (Join-Path $root '1.6\Defs') -Filter '*.xml' -File
)
foreach ($file in $scanFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($term in $forbidden) {
        if ($content.IndexOf($term, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Core source or Def file contains an optional Fishing Treasures reference: $($file.FullName) ($term)."
        }
    }
}

$assemblyPaths = @(
    (Join-Path $root '1.6\Assemblies\AquacultureFishing.dll')
)
foreach ($path in $assemblyPaths) {
    if (-not (Test-Path -LiteralPath $path)) { continue }
    $text = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($path))
    foreach ($term in $forbidden) {
        if ($text.IndexOf($term, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Built assembly contains an optional Fishing Treasures reference: $path ($term)."
        }
    }
}

$workshopPath = 'C:\Games\Steam\steamapps\workshop\content\294100\2468543398'
$state = if (Test-Path -LiteralPath $workshopPath) { 'installed' } else { 'not installed' }
Write-Host "Fishing Treasures metadata/source contract passed; optional content is $state."
