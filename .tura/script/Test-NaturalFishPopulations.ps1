param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
$ErrorActionPreference = 'Stop'
$populationPath = Join-Path $Root 'Source\NaturalFishPopulations.cs'
$ecologyPath = Join-Path $Root 'Source\FishEcology.cs'
$integrationPath = Join-Path $Root 'Source\FishingExpertise.cs'
$defsPath = Join-Path $Root '1.6\Defs\FishPopulationDefs.xml'
$architecturePath = Join-Path $Root 'architect.md'

$population = if (Test-Path $populationPath) { Get-Content -Raw $populationPath } else { '' }
$ecology = Get-Content -Raw $ecologyPath
$integration = Get-Content -Raw $integrationPath
$defs = if (Test-Path $defsPath) { Get-Content -Raw $defsPath } else { '' }
$architecture = Get-Content -Raw $architecturePath

$checks = [ordered]@{
    'serialized map population owner' = $population -match 'class NaturalFishPopulationMapComponent : MapComponent' -and $population -match 'Scribe_Collections.Look\(ref populations'
    'all natural bodies initialized' = $population -match 'InitializeAllWaterBodies' -and $population -match 'waterBodyTracker\?\.Bodies' -and
        $population -match 'map\.cellIndices\.NumGridCells'
    'connected water area not map area' = $population -match 'body\?\.cells' -and $population -match 'FloodFill' -and $population -notmatch 'map\.Area'
    'all six habitats supported' = @('Pond','Lake','River','Marsh','Coastal','Ocean') | ForEach-Object { $population -match $_ } | Where-Object { -not $_ } | Measure-Object | Select-Object -ExpandProperty Count | ForEach-Object { $_ -eq 0 }
    'moving water classified as river' = $population -match 'terrain\.Contains\("watermoving"\)'
    'species habitat def contract' = $ecology -match 'compatibleHabitats' -and $ecology -match 'habitatPreferences'
    'biome temperature season rarity density contract' = $ecology -match 'compatibleBiomes' -and $ecology -match 'populationMinimumTemperature' -and $ecology -match 'populationSeasons' -and $ecology -match 'populationRarity' -and $ecology -match 'populationDensity'
    'habitat scaling defs' = $defs -match 'FishPopulationHabitatDef' -and $defs -match '<populationPerCell>' -and $defs -match '<diversityCurve>' -and $defs -match '<carryingCapacityFactor>'
    'smooth diversity curve and viable species floor' = $population -match 'SimpleCurve diversityCurve' -and $population -match 'minimumViablePopulation' -and $population -match 'Evaluate\(Mathf\.Max\(0, cellCount\)\)'
    'coastal ocean threshold is def driven' = $defs -match '<minimumCells>800</minimumCells>' -and $population -match 'minimumCells'
    'deterministic stable hashing' = $population -match 'StableHash' -and $population -match 'WorldSeed' -and $population -notmatch 'Rand\.'
    'rarity weighted species selection' = $population -match 'RarityWeight' -and $population -match 'DeterministicRank'
    'framework imports preserve rarity bias' = $population -match 'PopulationWeight\(imported\[i\]' -and
        $population -match 'common\.Contains\(item\.FishDef\) \? 1f : 0\.25f' -and $population -match 'odysseyRarityInitialized'
    'dependency rarity and biome metadata reused' = $ecology -match 'DependencyCommonality' -and $ecology -match 'DependencyBiomeGroups' -and
        $population -match 'PopulationBiomeCompatible' -and $population -match 'PopulationCommonality'
    'habitat suitability is weighted once' = $population -match 'DeterministicRank\(ThingDef fish, NaturalWaterPopulation record\)' -and
        $population -match 'weight = Mathf\.Max\(0\.001f, PopulationWeight\(fish, record\.habitat\)\)'
    'carrying capacity enforced' = $population -match 'CarryingCapacity' -and $population -match 'Mathf\.Min\(.*capacity'
    'diversity scales smoothly with cells' = $population -match 'DiversityFor\(int cellCount\)' -and $population -match 'diversityCurve'
    'diversity cannot exceed viable population capacity' = $population -match 'Mathf\.Min\(habitatDef\?\.DiversityFor' -and
        $population -match 'Mathf\.FloorToInt\(record\.carryingCapacity /' -and
        $population -match 'Mathf\.Max\(habitatDef\.minimumViablePopulation, habitatDef\.minimumBreedingPopulation\)'
    'compatible viable records alone fill diversity target' = $population -match 'var selected = record\.species\.Where' -and
        $population -match 'candidates\.Contains\(item\.FishDef\)' -and $population -match 'item\.population >= 0\.5f' -and
        $population -match 'if \(selected\.Count >= diversity\) break'
    'initial capacity is divided at breeding viable floor' = $population -match 'Mathf\.Max\(habitatDef\?\.minimumViablePopulation' -and
        $population -match 'habitatDef\?\.minimumBreedingPopulation' -and $population -match 'viablePopulation \* selected\.Count'
    'incompatible imports do not consume generated diversity' = $population -match 'candidates\.Contains\(item\.FishDef\)' -and
        $population -match 'CompatibleCandidates\(record\)'
    'empty extension biome list retains dependency filtering' = $population -match 'compatibleBiomes\?\.Count \?\? 0\) == 0.*PopulationBiomeCompatible'
    'connected local variation' = $population -match 'LocalWeight' -and $population -match 'localVariation'
    'topology cache rebuild is event driven' = $population -match 'topologyRebuildTick' -and
        $population -match 'InitializeAllWaterBodies' -and $population -match 'populationByCell\.Clear\(\)'
    'terrain changes invalidate topology' = $population -match 'NaturalFishPopulationTerrainPatch' -and $population -match 'InvalidateTopology'
    'merged bodies combine records' = $population -match 'MergeConnectedRecords' -and $population -match 'target\.population \+= incoming\.population'
    'external depletion reconciled' = $population -match 'ReconcileExternalPopulation' -and $population -match 'expectedScalar - body\.Population'
    'existing populations retained' = $population -match 'ImportEstablished' -and $population -match 'item\.established' -and
        $population -match 'allocation = Mathf\.Max\(capacity, selected\.Sum\(item => item\.population\)\)' -and $population -match 'GradualRebalance'
    'framework migration is idempotent per species' = $population -match 'firstImport' -and
        $population -match 'item == null' -and $population -notmatch 'item\.population = Mathf\.Max\(item\.population, each\)'
    'framework lists cannot bypass migration after initialization' = $population -match 'allowNewSpecies = !record\.initialPopulationComplete' -and
        $population -notmatch 'allowNewSpecies = .*AllowsNaturalMigration'
    'faulty generated saves receive one-time diversity repair' = $population -match 'CurrentGenerationVersion = 1' -and
        $population -match 'UpgradePopulationGeneration' -and $population -match 'generationVersion < NaturalWaterPopulation\.CurrentGenerationVersion'
    'catch depletes shared record' = $integration -match 'NaturalFishPopulationMapComponent' -and $integration -match 'ConsumeCatch'
    'rivers coasts and oceans migrate naturally' = $population -match 'NaturalFishMigrationRules\.IsOpen' -and
        $population -match 'if \(AllowsNaturalMigration\(record\.habitat\)\)'
    'closed water extinction persists as tombstone' = $population -match 'item\.population >= habitatDef\.minimumBreedingPopulation' -and $population -match '!AllowsNaturalMigration\(record\.habitat\)' -and $population -match 'population < 0\.5f\) record\.species\[i\]\.population = 0f' -and $population -notmatch 'species\.RemoveAll\(item => item\.population'
    'explicit compatible stocking can restore extinction' = $population -match 'bool IntroduceFish' -and $population -match '!Suitable\(fishDef' -and $population -match 'item\.population \+= amount'
    'breeding requires viable population' = $population -match 'minimumBreedingPopulation' -and $population -match 'breedingPerDay' -and $population -match 'ApplyBreeding'
    'breeding replaces expected mortality before death' = $population -match 'expectedMortality = record\.TotalPopulation \* habitatDef\.naturalMortalityPerDay' -and
        $population.IndexOf('ApplyBreeding(record, habitatDef)') -lt $population.IndexOf('ApplyNaturalMortality(record, habitatDef)')
    'open migration is bounded and pressure aware' = $population -match 'ApplyMigration' -and $population -match 'migrationPerDay' -and $population -match 'maximumMigrationPerDay' -and $population -match 'populationPressure'
    'regional migration uses connected populations' = $population -match 'RegionalSpeciesPressure' -and $population -match 'populations.Where'
    'infrequent lifecycle update is def driven' = $population -match 'updateIntervalTicks' -and $population -match 'lastBalanceTick'
    'water mouseover lists prepared tracker species and estimates' = $population -match 'NaturalFishPopulationMouseoverPatch' -and
        $population -match 'TryGetPreparedSummary\(cell' -and $population -match 'summary\.readoutText'
    'fishing tab lists authoritative species estimates' = $population -match 'NaturalFishPopulationFishingTabPatch' -and
        $population -match 'PreparedCommonSpecies' -and $population -match 'summary\.DisplayLabel\(fish\)'
    'framework lists seed authoritative ledger' = $integration -match 'VfeSpecies' -and $integration -match 'OdysseySpecies' -and
        $integration -match 'Enumerable\.Empty<ThingDef>' -and $integration -notmatch 'VfeSpecies\(zone\)\.Contains'
    'architecture compatibility contract' = $architecture -match 'Old saves without population records' -and $architecture -match 'initialize lazily'
}
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value } | ForEach-Object Key)
if ($failed.Count) { throw ('Natural fish population regression failed: ' + ($failed -join '; ')) }
Write-Output ("Natural fish population regression passed ({0} checks)." -f $checks.Count)
