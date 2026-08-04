param([string]$Root = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))

$ErrorActionPreference = 'Stop'
$traits = Get-Content -Raw (Join-Path $Root '1.6\Defs\FishTraitDefs.xml')
$defs = Get-Content -Raw (Join-Path $Root '1.6\Defs\PondSwimmingDefs.xml')
$swimming = Get-Content -Raw (Join-Path $Root 'Source\PondSwimming.cs')
$life = Get-Content -Raw (Join-Path $Root 'Source\FishLifeCycle.cs')
$ecology = Get-Content -Raw (Join-Path $Root 'Source\FishEcology.cs')
$core = Get-Content -Raw (Join-Path $Root 'Source\AquacultureCore.cs')
$habitats = Get-Content -Raw (Join-Path $Root 'Source\PondHabitats.cs')
$names = @('Delicious','Beautiful','Nutritious','Healing','Playful','Calming','Energizing','Cleansing','Aromatic','Cooling','Warming','Curious','Therapeutic','Luxurious','Sparkling','Friendly','Grazer','Fertilizing','Oxygenator','Anima','Lunar')

$checks = [ordered]@{
    'all requested traits' = @($names | Where-Object { $traits -notmatch ('<label>' + [regex]::Escape($_) + '(?: \(\+%\))?</label>') }).Count -eq 0
    'def-driven effect schema' = $swimming -match 'class FishTraitPondEffect' -and $traits -match '<pondEffect>'
    'diminishing returns and caps' = $swimming -match 'Mathf.Sqrt' -and $swimming -match 'Mathf.Min'
    'single aggregate hediff' = $defs -match '<defName>AF_SpentTimeInPond</defName>' -and $swimming -match 'class Hediff_SpentTimeInPond'
    'per-effect duration persistence' = $swimming -match 'effectDurations' -and $swimming -match 'effectExpiresAt' -and
        $swimming -match 'Scribe_Collections\.Look\(ref effectExpiresAt, "pondEffectExpiries"' -and
        $swimming -match 'effectExpiresAt\.Where\(pair => pair\.Value <= now\)'
    'right-click order' = $swimming -match 'FloatMenuOptionProvider_EnterPond' -and $swimming -match 'Enter Pond'
    'bounded recreation job' = $swimming -match 'JobDriver_EnterPond' -and $swimming -match 'GainJoy' -and $defs -match '<joyDuration>'
    'safety and reachability' = $swimming -match 'SafeTemperatureAtCell' -and $swimming -match 'CanReserveAndReach' -and $swimming -match 'Drafted'
    'living fish only' = $swimming -match 'IsAlive' -and $swimming -match 'IsSwimmingInPond'
    'hediff refresh not duplicate' = $swimming -match 'GetFirstHediffOfDef' -and $swimming -match 'expiresAt'
    'food provenance propagation' = $life -match 'CompFishFoodTraits' -and $life -match 'InheritFoodTraits' -and $life -match 'nutritionMultiplier = Mathf.Max'
    'delicious ingestion mood' = $defs -match 'AF_AteDeliciousFish' -and $life -match 'PostIngested' -and $core -match 'PostIngested'
    'pond ecology traits' = $life -match 'PondTraitBeauty' -and $ecology -match 'ApplyPondTraitEcology'
    'fertilizing cached crop hook' = $swimming -match 'PlantGrowthRatePondTraitPatch' -and
        $life -match 'cropGrowthByCell' -and $swimming -match 'PondCropGrowthBonusAt\(IntVec3 cell\) => cropGrowthByCell\.TryGetValue'
    'oxygenator capacity hook' = $habitats -match 'PondTraitCapacityBonus' -and
        $habitats -match 'PoweredAeratorCapacity'
    'oxygenator health hook' = $swimming -match 'detritusReductionPerHour' -and $traits -match '<detritusReductionPerHour>'
    'night beauty refresh' = $life -match 'RefreshTimedPondTraitEffects' -and $swimming -match 'lastPondTraitNight'
    'existing inheritance retained' = $life -match 'BuildInheritance' -and $life -match 'inheritedTraits'
    'save compatibility' = $swimming -match 'Scribe_Collections.Look' -and $life -match 'Scribe_Values.Look\(ref nutritionMultiplier'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count -gt 0) { throw ('Pond swimming trait regression failed: ' + (($failed | ForEach-Object Key) -join ', ')) }
Write-Output ("Pond swimming trait regression passed ({0} checks)." -f $checks.Count)
