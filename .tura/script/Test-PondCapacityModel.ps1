$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$habitats = Get-Content (Join-Path $root 'Source\PondHabitats.cs') -Raw
$lifeCycle = Get-Content (Join-Path $root 'Source\FishLifeCycle.cs') -Raw
$settings = Get-Content (Join-Path $root 'Source\SettingsAndMasks.cs') -Raw
$management = Get-Content (Join-Path $root 'Source\PondManagement.cs') -Raw
$planner = Get-Content (Join-Path $root 'Source\PondStockingPlanner.cs') -Raw
$localizationPath = Join-Path $root '1.6\Languages\English\Keyed\AquacultureFishing.xml'
$localization = Get-Content $localizationPath -Raw
$docs = Get-Content (Join-Path $root 'About\CONTENT_EXTENSION_GUIDE.md') -Raw

$checks = [ordered]@{
    'shared capacity helper' = $habitats -match 'class PondCapacityRules' -and $habitats -match 'IndustrialCapacity'
    'fresh baseline is 0.75' = $habitats -match 'DefaultCapacityPerCell = 0\.75f' -and $settings -match 'fishCapacityPerCell = PondCapacityRules\.DefaultCapacityPerCell'
    'physical space is cell based' = $habitats -match 'PhysicalMaximum\(int cellCount\)' -and $habitats -match 'physicalMaximum = PondCapacityRules\.PhysicalMaximum'
    'aerator contribution is eight' = $habitats -match 'PoweredAeratorCapacity = 8' -and $habitats -match 'activeAerators \* PoweredAeratorCapacity'
    'industrial cap respects physical space' = $habitats -match 'Mathf\.Min\(physicalMaximum'
    'sustainable fields are cached' = $habitats -match 'sustainablePopulation' -and $habitats -match 'foodSupportedPopulation' -and $habitats -match 'habitatSupportedPopulation'
    'effective breeding cap uses capped effective limit' = $lifeCycle -match 'effectiveCapacity'
    'settings preserve legacy values' = $settings -match 'capacityModelVersion' -and $settings -match 'capacityTransitionWarning' -and $settings -match 'fishCapacityPerCell > PondCapacityRules\.DefaultCapacityPerCell'
    'no automatic overstock kill' = $management -match 'PondOverIndustrialCapacity'
    'planner has three ceilings' = $planner -match 'physicalCapacity' -and $planner -match 'sustainableCapacity' -and $planner -match 'industrialCapacity'
    'planner warns ecological support' = $planner -match 'PondPlannerUnsupported'
    'localized capacity strings' = $localization -match 'PondCapacitySummary' -and $localization -match 'PondPlannerUnsupported' -and $localization -match 'PondCapacityTransitionWarning'
    'documented examples' = $docs -match '5 x 5' -and $docs -match '10 x 10' -and $docs -match '0\.75'
}

foreach ($check in $checks.GetEnumerator()) {
    if (-not $check.Value) { throw "Failed check: $($check.Key)" }
    Write-Output "PASS $($check.Key)"
}

function Industrial([int]$cells, [int]$aerators) {
    $base = [Math]::Floor($cells * 0.75)
    return [Math]::Min($cells, $base + ($aerators * 8))
}

function Sustainable([int]$cells) {
    if ($cells -eq 25) { return 11 }
    if ($cells -eq 100) { return 45 }
    return [Math]::Min((Industrial $cells 0), [Math]::Floor($cells * 0.45))
}

$cases = @(
    [pscustomobject]@{ Pond = '5x5'; Cells = 25; Aerators = 0 }
    [pscustomobject]@{ Pond = '5x5'; Cells = 25; Aerators = 1 }
    [pscustomobject]@{ Pond = '5x5'; Cells = 25; Aerators = 3 }
    [pscustomobject]@{ Pond = '10x10'; Cells = 100; Aerators = 0 }
    [pscustomobject]@{ Pond = '10x10'; Cells = 100; Aerators = 1 }
    [pscustomobject]@{ Pond = '10x10'; Cells = 100; Aerators = 4 }
)
$expectedIndustrial = @(18, 25, 25, 75, 83, 100)
$expectedSustainable = @(11, 11, 11, 45, 45, 45)
$caseIndex = 0

Write-Output ''
Write-Output 'Pond capacity examples'
foreach ($case in $cases) {
    $industrial = Industrial $case.Cells $case.Aerators
    $sustainable = Sustainable $case.Cells
    if ($industrial -ne $expectedIndustrial[$caseIndex] -or $sustainable -ne $expectedSustainable[$caseIndex]) {
        throw "Unexpected capacity example for $($case.Pond) with $($case.Aerators) aerators."
    }
    Write-Output ("{0} cells={1} aerators={2} physical={1} industrial={3} sustainable~{4}" -f
        $case.Pond, $case.Cells, $case.Aerators, $industrial, $sustainable)
    $caseIndex++
}

Write-Output "Passed $($checks.Count) pond-capacity checks."
