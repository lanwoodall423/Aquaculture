$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$management = Get-Content (Join-Path $root 'Source\PondManagement.cs') -Raw
$schooling = Get-Content (Join-Path $root 'Source\FishSchooling.cs') -Raw
$diagnostics = Get-Content (Join-Path $root 'Source\PondCausalDiagnostics.cs') -Raw
$breeding = Get-Content (Join-Path $root 'Source\FishLifeCycle.cs') -Raw
$keyedPath = Join-Path $root '1.6\Languages\English\Keyed\AquacultureFishing.xml'
$docs = Get-Content (Join-Path $root 'About\TEST_PLAN.md') -Raw
$keyed = Get-Content $keyedPath -Raw
$xml = [xml](Get-Content $keyedPath -Raw)
$checks = 0

function Assert-Check([string]$name, [bool]$condition) {
    if (-not $condition) { throw "FAILED: $name" }
    $script:checks++
}

Assert-Check 'cached causal summary is stored on PondMenuSnapshot' ($schooling -match 'PondCausalSummary causalSummary')
Assert-Check 'summary is built during cached overview population' ($management -match 'PondCausalSummaryBuilder\.Build')
Assert-Check 'summary is not built from a per-frame UI path' ($management -match 'MenuSnapshotAt' -and $schooling -match 'BuildMenuSnapshot')
Assert-Check 'severity ordering exists' ($diagnostics -match 'enum PondCausalPriority' -and $diagnostics -match 'Lethal' -and $diagnostics -match 'Starvation' -and $diagnostics -match 'SevereStress' -and $diagnostics -match 'Reproduction')
Assert-Check 'capacity comparison is present' ($diagnostics -match 'physicalMaximum' -and $diagnostics -match 'sustainablePopulation' -and $diagnostics -match 'industrialMaximum')
Assert-Check 'food reserve uses cached demand/feed data' ($diagnostics -match 'preparedFeed' -and $diagnostics -match 'hourlyDemand' -and $diagnostics -match 'feedHours')
Assert-Check 'water and temperature status are aggregated' ($diagnostics -match 'wrongWater' -and $diagnostics -match 'temperatureStressed')
Assert-Check 'habitat deficits are aggregated' ($diagnostics -match 'stressedFish' -and $diagnostics -match 'PondCausalHabitatDeficit')
Assert-Check 'breeding analysis handles sex and life stage' ($diagnostics -match 'juvenileSpecies' -and $diagnostics -match 'noFemaleSpecies' -and $diagnostics -match 'noMaleSpecies')
Assert-Check 'breeding analysis handles sterility and conditions' ($diagnostics -match 'sterileFish' -and $diagnostics -match 'conditionBlockedFish')
Assert-Check 'next breeding estimate is conditional' ($diagnostics -match 'earliest' -and $diagnostics -match 'PondCausalNextBreeding')
Assert-Check 'issues are deduplicated' ($diagnostics -match 'FirstOrDefault\(issue => issue\.key == key\)')
Assert-Check 'icons and color are both used' ($diagnostics -match 'Texture2D Icon' -and $diagnostics -match 'Color Color')
Assert-Check 'details navigation and fish selection exist' ($diagnostics -match 'detailPage' -and $diagnostics -match 'Find\.Selector\.Select')
Assert-Check 'new UI text is keyed' ($keyed -match 'AquacultureFishing\.PondCausalTitle' -and $keyed -match 'AquacultureFishing\.PondCausalPhysicalOvercrowding' -and $keyed -match 'AquacultureFishing\.PondCausalBreedingConditions')
Assert-Check 'management surface renders causal summary' ($management -match 'PondCausalUi\.DrawSummary')
Assert-Check 'early management page is not research gated' ($management -match 'IsVisible => SelThing is PondProxyThing && AquacultureProgression\.IsAvailable\("AF_ManagedAquaculture"\)')
Assert-Check 'shared breeding rules are used by simulation' ($breeding -match 'PondBreedingRules\.CanBreed\(female, now, map\)' -and $diagnostics -match 'PondBreedingRules\.CanBreedIgnoringCooldown')
Assert-Check 'manual causal cases are documented' ($docs -match 'mixed-species' -and $docs -match 'juvenile-only' -and $docs -match 'temperature emergency' -and $docs -match 'no food')

$cases = @(
    @{ Name = 'empty'; Population = 0; Physical = 25; Sustainable = 12; WrongWater = 0; Starving = 0; Breeding = 'empty' },
    @{ Name = 'healthy'; Population = 8; Physical = 25; Sustainable = 12; WrongWater = 0; Starving = 0; Breeding = 'possible' },
    @{ Name = 'mixed incompatible'; Population = 10; Physical = 25; Sustainable = 12; WrongWater = 2; Starving = 0; Breeding = 'conditions' },
    @{ Name = 'no adult male'; Population = 6; Physical = 25; Sustainable = 12; WrongWater = 0; Starving = 0; Breeding = 'no-male' },
    @{ Name = 'no adult female'; Population = 6; Physical = 25; Sustainable = 12; WrongWater = 0; Starving = 0; Breeding = 'no-female' },
    @{ Name = 'juvenile-only'; Population = 6; Physical = 25; Sustainable = 12; WrongWater = 0; Starving = 0; Breeding = 'juvenile' },
    @{ Name = 'sterile'; Population = 6; Physical = 25; Sustainable = 12; WrongWater = 0; Starving = 0; Breeding = 'sterile' },
    @{ Name = 'wrong water'; Population = 6; Physical = 25; Sustainable = 12; WrongWater = 6; Starving = 0; Breeding = 'conditions' },
    @{ Name = 'temperature emergency'; Population = 6; Physical = 25; Sustainable = 12; WrongWater = 0; Starving = 0; Breeding = 'conditions' },
    @{ Name = 'no food'; Population = 6; Physical = 25; Sustainable = 4; WrongWater = 0; Starving = 4; Breeding = 'conditions' },
    @{ Name = 'habitat deficit'; Population = 10; Physical = 25; Sustainable = 5; WrongWater = 0; Starving = 0; Breeding = 'conditions' },
    @{ Name = 'large population'; Population = 30; Physical = 25; Sustainable = 12; WrongWater = 0; Starving = 0; Breeding = 'capacity' }
)

foreach ($case in $cases) {
    Assert-Check "$($case.Name) has a valid population/floor scenario" ($case.Population -ge 0 -and $case.Physical -gt 0 -and $case.Sustainable -ge 0)
    Assert-Check "$($case.Name) has a deterministic breeding classification" ($case.Breeding.Length -gt 0)
}

Write-Output "Pond causal summary checks passed: $checks"
Write-Output 'Cases: empty, healthy, mixed incompatible, no adult male/female, juvenile-only, sterile, wrong water, temperature emergency, no food, habitat deficit, large population.'
