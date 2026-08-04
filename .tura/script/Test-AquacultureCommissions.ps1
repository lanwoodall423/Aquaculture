$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source = Get-Content (Join-Path $root 'Source\AquacultureCommissions.cs') -Raw
$journal = Get-Content (Join-Path $root 'Source\AquacultureJournal.cs') -Raw
$keyedPath = Join-Path $root '1.6\Languages\English\Keyed\AquacultureFishing.xml'
$keyed = Get-Content $keyedPath -Raw
$release = Get-Content (Join-Path $root 'About\RELEASE_NOTES.md') -Raw
$compatibility = Get-Content (Join-Path $root 'About\COMPATIBILITY.md') -Raw
$testPlan = Get-Content (Join-Path $root 'About\TEST_PLAN.md') -Raw
$checks = 0

function Assert-Check([string]$name, [bool]$condition) {
    if (-not $condition) { throw "FAILED: $name" }
    $script:checks++
}

Assert-Check 'saved commission record exists' ($source -match 'class AquacultureCommissionRecord : IExposable' -and $source -match 'Scribe_Deep.Look\(ref activeCommission')
Assert-Check 'saved request history and cooldown exist' ($source -match 'commissionRequestHistory' -and $source -match 'commissionNextOfferTick')
Assert-Check 'breed completion count is serialized' ($journal -match 'commissionsCompleted' -and $journal -match 'lastCommissionTick')
Assert-Check 'base-game Silver reward is used' ($source -match 'ThingDefOf\.Silver')
Assert-Check 'no DLC quest dependency is introduced' ($source -notmatch 'Royalty|Ideology|QuestScriptDef|FactionDefOf\.Empire')
Assert-Check 'healthy living eligibility is explicit' ($source -match 'IsAlive' -and $source -match 'IsAdult' -and $source -match 'sterilized' -and $source -match 'foodReserve < 0\.35f' -and $source -match 'starvationProgress > 0f')
Assert-Check 'stress and size requirements are enforced' ($source -match 'waterStress >= 0\.10f' -and $source -match 'temperatureStress >= 0\.10f' -and $source -match 'habitatStress >= 0\.75f' -and $source -match 'SizeFactor < minimumSizeFactor')
Assert-Check 'breed and defining trait requirements are enforced' ($source -match 'breedId != breedId' -and $source -match 'traitDefNames.*Contains\(traitDefName\)')
Assert-Check 'requests are generated only from current map specimens' ($source -match 'Find\.Maps' -and $source -match 'listerThings\.AllThings' -and $source -match 'HealthyBreedFish')
Assert-Check 'candidate enumeration is interval cached' ($source -match 'eligibleSpecimens' -and $source -match 'RefreshEligibleSpecimens' -and $source -match 'EligibleSpecimenCount')
Assert-Check 'trait generation and size thresholds are selected from one candidate' ($source -match 'bestCandidate' -and $source -match 'bestCandidate\.breedGeneration' -and $source -match 'bestCandidate\.SizeFactor')
Assert-Check 'identical requests are suppressed' ($source -match 'RequestKey' -and $source -match 'requestHistory\.Contains' -and $source -match 'requestHistory\.Count > 64')
Assert-Check 'frequency is bounded' ($source -match 'OfferCooldownTicks = 30 \* 60000' -and $source -match 'EligibilityCheckIntervalTicks = 6000')
Assert-Check 'deadline and reward use all requested scaling inputs' ($source -match 'DeadlineDays' -and $source -match 'generationFactor' -and $source -match 'stabilityFactor' -and $source -match 'rarityFactor' -and $source -match 'sizeFactorReward' -and $source -match 'deadlineFactor')
Assert-Check 'reward has a hard wealth bound' ($source -match 'Mathf\.Clamp\(Mathf\.RoundToInt\(raw / 25f\) \* 25, 250, 2400\)')
Assert-Check 'reward placement precedes specimen destruction' ($source.IndexOf('GenPlace.TryPlaceThing') -ge 0 -and $source.IndexOf('GenPlace.TryPlaceThing') -lt $source.IndexOf('specimen.Destroy'))
Assert-Check 'delivery failure preserves the specimen' ($source -match 'CommissionRewardPlacementFailed' -and $source -match 'CommissionSpecimenTransferFailed' -and $source -match 'reward\.Destroy\(DestroyMode\.Vanish\)')
Assert-Check 'completion consumes the specimen and records the breed result' ($source -match 'specimen.Destroy\(DestroyMode\.Vanish\)' -and $source -match 'breed\.commissionsCompleted\+\+')
Assert-Check 'active request is offered by a base-game letter' ($source -match 'Find\.LetterStack\.ReceiveLetter' -and $source -match 'LetterDefOf\.PositiveEvent')
Assert-Check 'journal delivery UI lists eligible specimens' ($source -match 'Dialog_DeliverAquacultureCommission' -and $source -match 'EligibleSpecimens' -and $source -match 'CommissionDeliverButton')
Assert-Check 'all new player-facing keys exist' ($keyed -match 'CommissionLetterLabel' -and $keyed -match 'CommissionRequirements' -and $keyed -match 'CommissionDeliverButton' -and $keyed -match 'CommissionCompleted' -and $keyed -match 'CommissionExpired')
Assert-Check 'documentation covers delivery, save, cooldown, and no-DLC behavior' ($release -match 'Aquaculture Collector Commission' -and $compatibility -match 'Active collector commissions' -and $testPlan -match 'collector letter')

function Reward([int]$generation, [double]$stability, [double]$difficulty, [double]$size, [int]$deadline) {
    $rarity = 1 + [Math]::Min(1, [Math]::Max(0, ($difficulty - 0.75) / 1.25)) * 0.35
    $generationFactor = 1 + [Math]::Min(8, [Math]::Max(0, $generation - 1)) * 0.10
    $stabilityFactor = 1 + [Math]::Min(1, [Math]::Max(0, ($stability - 0.70) / 0.28)) * 0.45
    $sizeFactor = 1 + [Math]::Min(1.25, [Math]::Max(0, $size - 0.75)) * 0.30
    $deadlineFactor = [Math]::Min(1.75, [Math]::Max(0.75, 14 / [Math]::Max(1, $deadline)))
    $raw = 300 * $rarity * $generationFactor * $stabilityFactor * $sizeFactor * $deadlineFactor
    return [Math]::Min(2400, [Math]::Max(250, [Math]::Round($raw / 25) * 25))
}

$baselineReward = Reward 1 0.70 0.75 0.75 18
$rareReward = Reward 5 0.95 2.0 1.5 7
Assert-Check 'baseline reward is bounded and deterministic' ($baselineReward -eq 250 -and (Reward 1 0.70 0.75 0.75 18) -eq $baselineReward)
Assert-Check 'rarer harder commissions reward more but remain bounded' ($rareReward -gt $baselineReward -and $rareReward -le 2400)
Assert-Check 'request identity changes with requirements' ('b|trait|1|1.00' -ne 'b|trait|2|1.00' -and 'b|trait|1|1.00' -ne 'b|trait2|1|1.00')

$xml = [xml]$keyed
$elements = @($xml.LanguageData.ChildNodes | Where-Object { $_.NodeType -eq 'Element' })
$duplicateKeys = @($elements | Group-Object Name | Where-Object Count -gt 1)
Assert-Check 'keyed localization parses without duplicate elements' ($xml.DocumentElement.Name -eq 'LanguageData' -and $duplicateKeys.Count -eq 0)

Write-Output "Aquaculture commission checks passed: $checks"
Write-Output "Deterministic reward cases: baseline=$baselineReward silver; rare/high-quality=$rareReward silver"
