param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
$source = Get-Content -Raw (Join-Path $Root 'Source\NaturalFishPopulations.cs')

$checks = [ordered]@{
    'prepared reads use the cell index only' = $source -match 'TryGetPreparedSummary\(IntVec3 cell' -and
        $source -match 'populationByCell\.TryGetValue\(cell, out NaturalWaterPopulation record\)'
    'cached population lookup avoids topology validation' = $source -match 'if \(populationByCell\.TryGetValue\(cell, out NaturalWaterPopulation cached\)\)' -and
        $source -match 'return cached;'
    'ui reads prepared summaries' = $source -match 'PreparedSummaryFor\(WaterBody body\)' -and
        $source -match 'TryGetPreparedSummary\(body\.rootCell'
    'ui does not resolve populations' = $source -notmatch 'DrawPopulationReadout[\s\S]{0,700}PopulationAt\(cell\)'
    'fishing tab uses authoritative species' = $source -match 'NaturalFishPopulationFishingTabPatch' -and
        $source -match 'PreparedCommonSpecies' -and $source -match 'PreparedUncommonSpecies'
    'fishing rows show estimated counts' = $source -match 'NaturalFishPopulationFishingRowPatch' -and
        $source -match 'DisplayLabel\(fish\)'
    'fishing tab grows for complete species list' = $source -match 'public static void Prefix\(ITab_Fishing __instance\)' -and
        $source -match 'activeSummary\?\.presentSpecies\.Count \?\? -1'
    'fishing threshold skips scalar tracker when prepared' = $source -match 'NaturalFishPopulationFishingThresholdPatch' -and
        $source -match '__result = summary\.totalPopulation' -and $source -match 'return false;'
    'tab formatting and sizing are event prepared' = $source -match 'summary\.displayLabels\[fish\] = displayLabel' -and
        $source -match 'if \(speciesCount == sizedSpeciesCount\) return;'
    'species rows reuse active prepared summary' = $source -match 'NaturalFishPopulationSummary summary = NaturalFishPopulationFishingTabPatch\.ActiveSummary' -and
        $source -match 'if \(summary == null\)[\s\S]{0,500}PreparedSummaryFor\(body\)'
    'summary refreshes after catch' = $source -match 'ConsumeCatch[\s\S]{0,700}RefreshSummary\(record\)'
    'summary refreshes after stocking' = $source -match 'IntroduceFish[\s\S]{0,1800}RefreshSummary\(record\)'
    'summary refreshes after lifecycle' = $source -match 'GradualRebalance[\s\S]{0,1800}RefreshSummary\(record\)'
    'lifecycle scheduling uses record intervals' = $source -match 'ScheduleNextBalance' -and
        $source -notmatch 'nextBalanceTick = tick \+ 2500'
    'stable anchor is linear' = $source -match 'foreach \(IntVec3 cell in cells\)' -and
        $source -notmatch 'OrderBy\(cell => cell\.z\)\.ThenBy\(cell => cell\.x\)\.First'
    'tooltip uses cached readout text' = $source -match 'summary\.readoutText' -and
        $source -notmatch 'DrawPopulationReadout[\s\S]{0,1000}OrderByDescending'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value } | ForEach-Object Key)
if ($failed.Count) { throw ('Natural fish performance regression failed: ' + ($failed -join '; ')) }
Write-Output ("Natural fish performance regression passed ({0} checks)." -f $checks.Count)
