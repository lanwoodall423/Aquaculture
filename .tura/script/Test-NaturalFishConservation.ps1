param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
$ErrorActionPreference = 'Stop'

$populationPath = Join-Path $Root 'Source\NaturalFishPopulations.cs'
$fishingPath = Join-Path $Root 'Source\FishingExpertise.cs'
$localizationPath = Join-Path $Root '1.6\Languages\English\Keyed\AquacultureFishing.xml'
$guidePath = Join-Path $Root 'About\CONTENT_EXTENSION_GUIDE.md'
$testPlanPath = Join-Path $Root 'About\TEST_PLAN.md'
$population = Get-Content -Raw $populationPath
$fishing = Get-Content -Raw $fishingPath
$localization = Get-Content -Raw $localizationPath
$guide = Get-Content -Raw $guidePath
$testPlan = Get-Content -Raw $testPlanPath
$warningIndex = $fishing.IndexOf('WarnIfCatchLikelyCrossesBreedingFloor')
$biteResolutionIndex = $fishing.IndexOf('bool fishBit =')

$checks = [ordered]@{
    'status model fields' = $population -match 'NaturalFishConservationStatus' -and
        $population -match 'approximatePopulation' -and $population -match 'breedingFloor' -and
        $population -match 'breedingPossible' -and $population -match 'catchLikelyCrossesBreedingFloor'
    'direction model' = $population -match 'Growing' -and $population -match 'Stable' -and
        $population -match 'Declining' -and $population -match 'BelowBreedingPopulation'
    'migration context model' = $population -match 'OpenWithoutRecordedSource' -and
        $population -match 'OpenWithRecordedSource' -and $population -match 'MigrationCanRecover'
    'summary caches tracked species' = $population -match 'trackedSpecies' -and
        $population -match 'conservationBySpecies' -and $population -match 'BuildConservationStatus'
    'summary uses approximate values' = $population -match 'Mathf\.RoundToInt\(population\)' -and
        $population -match 'ConservationPopulation'
    'floor and breeding calculation' = $population -match 'minimumBreedingPopulation' -and
        $population -match 'breedingPossible' -and $population -match 'availableGrowth'
    'catch risk calculation' = $population -match 'population - 1f < breedingFloor'
    'migration recovery context' = $population -match 'MigrationPressureFor' -and
        $population -match 'RegionalSpeciesPressure\(record, fishDef\)'
    'cached UI lookup' = $population -match 'TryGetConservationSummary' -and
        $population -match 'TryGetPreparedSummary'
    'load and topology invalidation' = $population -match 'conservationWarningKeys\.Clear\(\)' -and
        $population -match 'InvalidateTopology'
    'catch warning deduplication' = $population -match 'WarnIfCatchLikelyCrossesBreedingFloor' -and
        $population -match 'conservationWarningKeys\.Add' -and
        $population -match 'MessageTypeDefOf\.CautionInput'
    'warning called before bite resolution' = $warningIndex -ge 0 -and
        $biteResolutionIndex -gt $warningIndex
    'selection excludes extinct ledger species' = $fishing -match 'Contains\(attempt\.waterCell, fish\)'
    'status tooltip is keyed' = $population -match 'TooltipHandler\.TipRegion' -and
        $population -match 'status\.Tooltip'
    'status uses icons and color' = $population -match 'TexButton\.Plus' -and
        $population -match 'TexButton\.Minus' -and $population -match 'TexButton\.Suspend' -and
        $population -match 'DrawTextureFitted'
    'all conservation localization keys' = $localization -match 'ConservationWaterTotal' -and
        $localization -match 'ConservationPopulation' -and $localization -match 'ConservationCatchWarning' -and
        $localization -match 'ConservationCatchRiskBelow' -and
        $localization -match 'ConservationMigrationClosed' -and
        $localization -match 'ConservationMigrationSourceRiver' -and
        $localization -match 'ConservationMigrationNoSourceCoastal'
    'closed recovery explanation documented' = $guide -match 'Closed Pond, Lake, and Marsh' -and
        $guide -match 'local breeding or explicit stocking'
    'open recovery explanation documented' = $guide -match 'compatible same-category source population' -and
        $guide -match 'bounded migration'
    'floor and recovery cases documented' = $testPlan -match 'exactly at the breeding floor' -and
        $testPlan -match 'River, Coastal body, and Ocean' -and $testPlan -match 'one caution message'
}

function Get-ConservationExpectation {
    param(
        [double]$Population,
        [double]$Floor,
        [bool]$MigrationEnabled,
        [bool]$CompatibleSource,
        [double]$NetChange = 0
    )
    $below = $Population -lt $Floor
    $extinct = $Population -lt 0.5
    $risk = $Population -gt 0 -and ($Population - 1) -lt $Floor
    [pscustomobject]@{
        Approximate = [math]::Round($Population)
        Breeding = (-not $below) -and $Population -ge 0.5
        Direction = if ($below) { 'Below' } elseif ($NetChange -gt [math]::Max(0.25, $Population * 0.02)) { 'Growing' } elseif ($NetChange -lt -[math]::Max(0.25, $Population * 0.02)) { 'Declining' } else { 'Stable' }
        Risk = $risk
        Extinct = $extinct
        MigrationRecovery = $MigrationEnabled -and $CompatibleSource
    }
}

$cases = @(
    [pscustomobject]@{ Name = 'closed at floor'; Habitat = 'Pond'; Population = 3; Floor = 3; Net = 0; Open = $false; Source = $false; ExpectedDirection = 'Stable'; ExpectedRisk = $true; ExpectedRecovery = $false },
    [pscustomobject]@{ Name = 'closed one above floor'; Habitat = 'Lake'; Population = 4; Floor = 3; Net = 0; Open = $false; Source = $false; ExpectedDirection = 'Stable'; ExpectedRisk = $false; ExpectedRecovery = $false },
    [pscustomobject]@{ Name = 'closed below floor'; Habitat = 'Marsh'; Population = 2; Floor = 3; Net = 0; Open = $false; Source = $false; ExpectedDirection = 'Below'; ExpectedRisk = $true; ExpectedRecovery = $false },
    [pscustomobject]@{ Name = 'closed extinct'; Habitat = 'Pond'; Population = 0; Floor = 3; Net = 0; Open = $false; Source = $false; ExpectedDirection = 'Below'; ExpectedRisk = $false; ExpectedRecovery = $false },
    [pscustomobject]@{ Name = 'river migration recovery'; Habitat = 'River'; Population = 2; Floor = 3; Net = 0; Open = $true; Source = $true; ExpectedDirection = 'Below'; ExpectedRisk = $true; ExpectedRecovery = $true },
    [pscustomobject]@{ Name = 'coastal migration recovery'; Habitat = 'Coastal'; Population = 2; Floor = 3; Net = 0; Open = $true; Source = $true; ExpectedDirection = 'Below'; ExpectedRisk = $true; ExpectedRecovery = $true },
    [pscustomobject]@{ Name = 'ocean without source'; Habitat = 'Ocean'; Population = 2; Floor = 3; Net = 0; Open = $true; Source = $false; ExpectedDirection = 'Below'; ExpectedRisk = $true; ExpectedRecovery = $false },
    [pscustomobject]@{ Name = 'growing direction'; Habitat = 'River'; Population = 5; Floor = 3; Net = 1; Open = $true; Source = $true; ExpectedDirection = 'Growing'; ExpectedRisk = $false; ExpectedRecovery = $true },
    [pscustomobject]@{ Name = 'declining direction'; Habitat = 'Coastal'; Population = 5; Floor = 3; Net = -1; Open = $true; Source = $false; ExpectedDirection = 'Declining'; ExpectedRisk = $false; ExpectedRecovery = $false }
)

foreach ($case in $cases) {
    $result = Get-ConservationExpectation $case.Population $case.Floor $case.Open $case.Source $case.Net
    $checks["case $($case.Name)"] = $result.Direction -eq $case.ExpectedDirection -and
        $result.Risk -eq $case.ExpectedRisk -and $result.MigrationRecovery -eq $case.ExpectedRecovery
    Write-Output ('{0}: {1} habitat, ~{2}, floor {3}, {4}, risk={5}, migration-recovery={6}' -f
        $case.Name, $case.Habitat, $result.Approximate, $case.Floor, $result.Direction, $result.Risk, $result.MigrationRecovery)
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value } | ForEach-Object Key)
if ($failed.Count) { throw ('Natural fish conservation contract failed: ' + ($failed -join '; ')) }
Write-Output ("Natural fish conservation contract passed ({0} checks)." -f $checks.Count)
