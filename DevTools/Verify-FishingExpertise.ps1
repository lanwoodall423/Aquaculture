param([string]$Root = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
$checks = [ordered]@{
    'architecture contract' = 'About\FISHING_EXPERTISE_ARCHITECTURE.md'
    'progression implementation' = 'Source\FishingExpertise.cs'
    'settings implementation' = 'Source\SettingsAndMasks.cs'
    'journal implementation' = 'Source\AquacultureJournal.cs'
}

foreach ($entry in $checks.GetEnumerator()) {
    $path = Join-Path $Root $entry.Value
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing $($entry.Key): $path" }
}

$expertise = Get-Content -Raw (Join-Path $Root 'Source\FishingExpertise.cs')
$settings = Get-Content -Raw (Join-Path $Root 'Source\SettingsAndMasks.cs')
$journal = Get-Content -Raw (Join-Path $Root 'Source\AquacultureJournal.cs')
$core = Get-Content -Raw (Join-Path $Root 'Source\AquacultureCore.cs')

$requirements = [ordered]@{
    'additive attempt save key' = $expertise -match 'aquacultureFishingAttempts'
    'additive pawn progression save key' = $expertise -match 'aquacultureFishingProgression'
    'species knowledge keyed by def name' = $expertise -match 'speciesKnowledge'
    'shared four expertise states' = $expertise -match 'KnowledgeRank' -and $expertise -match 'KnowledgeRanks\.ForExperience'
    'animals contributes' = $expertise -match 'SkillDefOf\.Animals'
    'VFE water list is rechecked' = $expertise -match 'fishInThisZone'
    'Odyssey ledger species is rechecked' = $expertise -match 'SpeciesAtOdyssey' -and $expertise -match 'OdysseyStillAvailable'
    'VFE job and completion hooks' = $core -match 'VCE_Fishing.JobDriver_Fish' -and $core -match 'VceCatchPrefix'
    'Odyssey result hook' = $core -match 'OdysseyCatchesPrefix'
    'rodless attempt rejected before start' = $core -match 'FishingJobPostfix' -and $core -match 'NonScanJob' -and $expertise -match 'MinimumExpertiseFor'
    'dynamic fish discovery retained' = $core -match 'AllDefs\.Where\(FishUtility\.IsFish\)'
    'per-species minimum setting' = $settings -match 'minimumFishingExpertise'
    'expertise journal tab' = $journal -match 'JournalPage\.Expertise'
    'breeds research gate' = $journal -match 'AF_SelectiveBreeding'
    'legacy species key retained' = $journal -match 'aquacultureSpeciesJournal'
    'legacy breeds key retained' = $journal -match 'aquacultureBreeds'
}

$failed = @($requirements.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count -gt 0) {
    $names = ($failed | ForEach-Object Key) -join ', '
    throw "Fishing expertise verification failed: $names"
}

Write-Output ("Fishing expertise verification passed ({0} checks)." -f $requirements.Count)
