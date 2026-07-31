param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
$ErrorActionPreference = 'Stop'
[xml]$xml = Get-Content -Raw (Join-Path $Root '1.6\Defs\FishPopulationDefs.xml')
$defs = @{}
foreach ($node in $xml.Defs.'AquacultureFishing.FishPopulationHabitatDef') { $defs[$node.habitat] = $node }

function Measure-Habitat([string]$habitat, [int]$cells) {
    $def = $defs[$habitat]
    if ($null -eq $def) { throw "Missing habitat def: $habitat" }
    $capacity = [Math]::Round($cells * [double]$def.populationPerCell * [double]$def.carryingCapacityFactor)
    $capacity = [Math]::Max([int]$def.minimumPopulation, [Math]::Min([int]$def.maximumPopulation, $capacity))
    $points = @($def.diversityCurve.points.li | ForEach-Object {
        $parts = ([string]$_).Trim('(', ')').Split(',')
        [pscustomobject]@{ Cells = [double]$parts[0]; Species = [double]$parts[1] }
    }) | Sort-Object Cells
    if ($points.Count -lt 2) { throw "Habitat $habitat needs a smooth diversity curve." }
    $lower = $points[0]
    $upper = $points[$points.Count - 1]
    foreach ($point in $points) {
        if ($point.Cells -le $cells) { $lower = $point }
        if ($point.Cells -ge $cells) { $upper = $point; break }
    }
    $fraction = if ($upper.Cells -gt $lower.Cells) { ($cells - $lower.Cells) / ($upper.Cells - $lower.Cells) } else { 0 }
    $target = $lower.Species + (($upper.Species - $lower.Species) * $fraction)
    $diversity = [Math]::Round($target)
    $diversity = [Math]::Max(0, [Math]::Min([int]$def.maximumDiversity, [Math]::Min([Math]::Floor($capacity / [double]$def.minimumViablePopulation), $diversity)))
    [pscustomobject]@{ Habitat = $habitat; Cells = $cells; Capacity = $capacity; Diversity = $diversity }
}

$samples = @(10, 24, 25, 69, 70, 149, 150, 299, 300, 499, 500, 800) | ForEach-Object { Measure-Habitat Lake $_ }
foreach ($sample in $samples) {
    if ($sample.Diversity -gt [Math]::Floor($sample.Capacity / [double]$defs.Lake.minimumViablePopulation)) { throw 'Diversity exceeds viable population support.' }
}
if ((Measure-Habitat Lake 24).Diversity -gt 1) { throw 'Under 25 cells must support at most one species.' }
if ((Measure-Habitat Lake 25).Diversity -lt 1 -or (Measure-Habitat Lake 69).Diversity -gt 2) { throw '25-69 cell diversity band failed.' }
if ((Measure-Habitat Lake 70).Diversity -lt 2 -or (Measure-Habitat Lake 149).Diversity -gt 3) { throw '70-149 cell diversity band failed.' }
if ((Measure-Habitat Lake 150).Diversity -lt 3 -or (Measure-Habitat Lake 299).Diversity -gt 5) { throw '150-299 cell diversity band failed.' }
if ((Measure-Habitat Lake 300).Diversity -lt 4 -or (Measure-Habitat Lake 499).Diversity -gt 7) { throw '300-499 cell diversity band failed.' }
if ((Measure-Habitat Lake 500).Diversity -lt 6) { throw '500 cells must be treated as a large source with at least six compatible species.' }
if ((Measure-Habitat Lake 266).Diversity -lt 3 -or (Measure-Habitat Lake 266).Diversity -gt 5) { throw 'A 266-cell lake must target three to five compatible species.' }
$lake266 = Measure-Habitat Lake 266
if ([Math]::Floor($lake266.Capacity / [double]$defs.Lake.minimumBreedingPopulation) -lt $lake266.Diversity) { throw 'A 266-cell lake cannot give every selected species a breeding-viable population.' }
$candidateCount = 6
$selected = @('compatible-present')
foreach ($candidate in @('compatible-present', 'compatible-two', 'compatible-three', 'compatible-four', 'compatible-five', 'compatible-six')) {
    if ($selected.Count -ge [Math]::Min($lake266.Diversity, $candidateCount)) { break }
    if ($candidate -notin $selected) { $selected += $candidate }
}
if ($selected.Count -ne $lake266.Diversity) { throw 'Imported incompatible or sub-present ledger entries prevented the 266-cell target from being filled.' }
if (($lake266.Capacity / $selected.Count) -lt [double]$defs.Lake.minimumBreedingPopulation) { throw 'The 266-cell allocation is not breeding viable.' }
$weights = @(1.0, 0.8, 0.32, 0.08)
$breedingFloor266 = [double][string]$defs.Lake.minimumBreedingPopulation
$reserved266 = $breedingFloor266 * $weights.Count
$remaining266 = $lake266.Capacity - $reserved266
$weightTotal266 = ($weights | Measure-Object -Sum).Sum
$allocation266 = @($weights | ForEach-Object { $breedingFloor266 + ($remaining266 * $_ / $weightTotal266) })
if ($allocation266.Count -ne $lake266.Diversity -or ($allocation266 | Measure-Object -Minimum).Minimum -lt $breedingFloor266) {
    throw 'The 266-cell rarity-weighted allocation did not preserve every breeding floor.'
}
if ([Math]::Abs((($allocation266 | Measure-Object -Sum).Sum) - $lake266.Capacity) -gt 0.001) { throw 'The 266-cell allocation did not consume carrying capacity exactly.' }
if ((Measure-Habitat Lake 70).Diversity -lt 2 -or (Measure-Habitat Lake 100).Diversity -gt 3) { throw 'A roughly 70-cell source must support two to three species.' }
$capacities = @(25, 70, 150, 300, 500, 800) | ForEach-Object { (Measure-Habitat Lake $_).Capacity }
for ($i = 1; $i -lt $capacities.Count; $i++) { if ($capacities[$i] -le $capacities[$i - 1]) { throw 'Population capacity is not monotonic by connected-water scale.' } }
foreach ($habitat in @('Pond', 'Lake', 'Marsh')) {
    if ([double]$defs[$habitat].migrationPerDay -ne 0 -or [double]$defs[$habitat].maximumMigrationPerDay -ne 0) { throw "$habitat must not migrate naturally." }
}
foreach ($habitat in @('River', 'Coastal', 'Ocean')) {
    if ([double]$defs[$habitat].migrationPerDay -le 0 -or [double]$defs[$habitat].maximumMigrationPerDay -le 0) { throw "$habitat must support bounded natural migration." }
}
foreach ($habitat in $defs.Keys) {
    if ([int]$defs[$habitat].updateIntervalTicks -lt 60000) { throw "$habitat lifecycle updates are too frequent." }
    if ([double]$defs[$habitat].minimumBreedingPopulation -lt [double]$defs[$habitat].minimumViablePopulation) { throw "$habitat breeding threshold is below viability." }
}

$closedDef = $defs.Lake
$breedingFloor = [double][string]$closedDef.minimumBreedingPopulation
$breedingRate = [double][string]$closedDef.breedingPerDay
$mortalityRate = [double][string]$closedDef.naturalMortalityPerDay
[double]$closedPopulation = $breedingFloor
for ($day = 0; $day -lt 120; $day++) {
    [double]$expectedMortality = $closedPopulation * $mortalityRate
    [double]$available = [Math]::Max(0.0, 70.0 - $closedPopulation + $expectedMortality)
    [double]$growth = if ($closedPopulation -ge $breedingFloor) {
        [Math]::Min($available, $closedPopulation * $breedingRate)
    } else { 0.0 }
    $closedPopulation = ($closedPopulation + $growth) * (1 - $mortalityRate)
}
if ($closedPopulation -lt $breedingFloor) { throw 'Viable closed-water founders collapse from lifecycle ordering.' }
$extinctPopulation = 0
for ($day = 0; $day -lt 120; $day++) { $extinctPopulation *= (1 - $mortalityRate) }
if ($extinctPopulation -ne 0) { throw 'Closed-water extinction returned without explicit stocking.' }

$riverDef = $defs.River
$migrationBudget = [Math]::Min([double]$riverDef.maximumMigrationPerDay, 70 * [double]$riverDef.migrationPerDay)
if ($migrationBudget -le 0 -or $migrationBudget -gt [double]$riverDef.maximumMigrationPerDay) { throw 'River migration budget is invalid.' }

$source = Get-Content -Raw (Join-Path $Root 'Source\NaturalFishPopulations.cs')
if ($source -notmatch 'Rare \? 0\.08f' -or $source -notmatch 'Uncommon \? 0\.32f : 1f') { throw 'Rarity preference weights changed unexpectedly.' }
Write-Output ("Natural fish scaling passed: 70 cells {0}/{1}, 500 cells {2}/{3} (population/species)." -f
    (Measure-Habitat Lake 70).Capacity, (Measure-Habitat Lake 70).Diversity,
    (Measure-Habitat Lake 500).Capacity, (Measure-Habitat Lake 500).Diversity)
