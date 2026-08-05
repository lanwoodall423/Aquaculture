param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
$ErrorActionPreference = 'Stop'
$populationPath = Join-Path $Root 'Source\NaturalFishPopulations.cs'
$recoveryPath = Join-Path $Root 'Source\NaturalFishRecoveryRules.cs'
$architecturePath = Join-Path $Root 'architect.md'
$guidePath = Join-Path $Root 'About\CONTENT_EXTENSION_GUIDE.md'
$localizationPath = Join-Path $Root '1.6\Languages\English\Keyed\AquacultureFishing.xml'
$population = Get-Content -Raw $populationPath
$recovery = Get-Content -Raw $recoveryPath
$architecture = Get-Content -Raw $architecturePath
$guide = Get-Content -Raw $guidePath
$localization = Get-Content -Raw $localizationPath

$habitats = @('Pond', 'Lake', 'River', 'Marsh', 'Coastal', 'Ocean')
$open = @('River', 'Coastal', 'Ocean')
$migrationCompatible = {
    param([string]$from, [string]$to)
    return ($open -contains $from) -and ($from -eq $to)
}
$checks = [ordered]@{
    'explicit migration rule helper' = $recovery -match 'class NaturalFishMigrationRules' -and
        $recovery -match 'IsOpen' -and $recovery -match 'AreCompatible' -and
        $recovery -match 'IsOpen\(first\)\s*&&\s*first\s*==\s*second'
    'closed habitats are documented' = $guide -match 'Pond, lake, and marsh are closed categories' -and
        $architecture -match 'Pond, lake, and marsh records are closed'
    'coastal is documented as open' = $guide -match 'Coastal-to-Coastal' -and
        $architecture -match 'Coastal records are open'
    'deterministic regional pressure' = $population -match 'RegionalSpeciesPressure' -and
        $population -match 'populations\.Where' -and $population -match 'CompatibleCandidates' -and
        $population -match 'MigrationRegionCompatible\(record\.habitat, other\.habitat\)'
    'migration help is localized' = $population -match 'MigrationRuleText' -and
        $localization -match 'AquacultureFishing\.MigrationRule' -and
        $localization -match 'AquacultureFishing\.MigrationClosed' -and
        $localization -match 'AquacultureFishing\.MigrationRiver' -and
        $localization -match 'AquacultureFishing\.MigrationCoastal' -and
        $localization -match 'AquacultureFishing\.MigrationOcean'
}

foreach ($from in $habitats) {
    foreach ($to in $habitats) {
        $key = "$from-$to"
        $expected = ($open -contains $from) -and ($from -eq $to)
        $checks["pair $key"] = (& $migrationCompatible $from $to) -eq $expected
    }
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value } | ForEach-Object Key)
if ($failed.Count) { throw ('Natural fish migration contract failed: ' + ($failed -join '; ')) }
Write-Output ("Natural fish migration contract passed ({0} checks; all 36 habitat pairings evaluated)." -f $checks.Count)
