$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$thingDefsPath = Join-Path $root '1.6\Defs\ThingDefs.xml'
$rodDefsPath = Join-Path $root '1.6\Defs\FishingRodDefs.xml'
$thingXml = [xml](Get-Content $thingDefsPath -Raw)
$rodXml = [xml](Get-Content $rodDefsPath -Raw)
$matrixPath = Join-Path $root 'About\VISUAL_ASSET_MATRIX.md'
$matrix = Get-Content $matrixPath -Raw
$checks = 0

function Assert-Check([string]$name, [bool]$condition) {
    if (-not $condition) { throw "FAILED: $name" }
    $script:checks++
}

$expected = @(
    'AF_FishFeed', 'AF_FishMeat', 'AF_FishEgg', 'AF_DuckweedCulture', 'AF_SeaLettuceCulture',
    'AF_DaphniaCulture', 'AF_BrineShrimpCulture', 'AF_PondSnailCulture', 'AF_FreshwaterMusselCulture',
    'AF_BlueMusselCulture', 'AF_PondLifeSamplingStation', 'AF_FishingBucket', 'AF_FishingBarrel',
    'AF_PondFeeder', 'AF_AutomaticPondFeeder', 'AF_AquaticPlantCover', 'AF_PondRockShelter',
    'AF_PreparedPondSubstrate', 'AF_PondAerator', 'AF_PondProxy', 'AF_FishingRod'
)
$thingNodes = @($thingXml.Defs.ThingDef | Where-Object { $_.defName }) + @($rodXml.Defs.ThingDef | Where-Object { $_.defName })
$names = @($thingNodes | ForEach-Object { $_.defName })
foreach ($defName in $expected) {
    Assert-Check "matrix includes loaded ThingDef $defName" ($names -contains $defName -and $matrix -match [regex]::Escape($defName))
}

$thingText = Get-Content $thingDefsPath -Raw
Assert-Check 'only existing owned production texture is wired' ($rodXml.Defs.ThingDef.graphicData.texPath -eq 'Things/Item/Equipment/FishingRod')
Assert-Check 'pond proxy remains procedural and textureless' ($thingText -match '<defName>AF_PondProxy</defName>[\s\S]*?<drawerType>None</drawerType>' -and $thingText -notmatch '<defName>AF_PondProxy</defName>[\s\S]*?<graphicData>')
Assert-Check 'manual and automatic feeders are explicitly distinguished in briefs' ($matrix -match 'PondFeeder' -and $matrix -match 'AutomaticPondFeeder' -and $matrix -match 'manual hopper' -and $matrix -match 'motor housing')
Assert-Check 'bucket and barrel silhouettes are explicitly distinguished' ($matrix -match 'FishingBucket' -and $matrix -match 'FishingBarrel' -and $matrix -match 'wider than the bucket')
Assert-Check 'culture products have distinct briefs' ($matrix -match 'DuckweedCulture' -and $matrix -match 'SeaLettuceCulture' -and $matrix -match 'DaphniaCulture' -and $matrix -match 'BlueMusselCulture')
Assert-Check 'habitat structures have distinct briefs' ($matrix -match 'AquaticPlantCover' -and $matrix -match 'PondRockShelter' -and $matrix -match 'PreparedPondSubstrate')
Assert-Check 'product and egg briefs exist' ($matrix -match 'FishFeed' -and $matrix -match 'FishMeat' -and $matrix -match 'FishEgg')
Assert-Check 'rotation, overlays, and blueprint checks are documented' ($matrix -match 'Rotation' -and $matrix -match 'snow' -and $matrix -match 'Blueprint/ghost')
Assert-Check 'case-sensitive path checks are documented' ($matrix -match 'case-sensitive')
Assert-Check 'no placeholder art is claimed' ($matrix -match 'rather than replaced with placeholder PNGs')

$rodPath = Join-Path $root '1.6\Textures\Things\Item\Equipment\FishingRod.png'
Assert-Check 'existing fishing rod texture exists at exact case' (Test-Path -LiteralPath $rodPath)
Add-Type -AssemblyName System.Drawing
$image = [System.Drawing.Bitmap]::FromFile($rodPath)
$transparent = 0
for ($x = 0; $x -lt $image.Width; $x++) {
    for ($y = 0; $y -lt $image.Height; $y++) {
        if ($image.GetPixel($x, $y).A -eq 0) { $transparent++ }
    }
}
Assert-Check 'fishing rod uses the established 500px transparent source' ($image.Width -eq 500 -and $image.Height -eq 500 -and $transparent -gt 0)
$image.Dispose()

Write-Output "Visual asset matrix checks passed: $checks"
Write-Output 'Owned production art: FishingRod.png only. All other missing art remains a production brief, not a placeholder.'
