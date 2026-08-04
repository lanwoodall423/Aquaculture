param([string]$Root = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))

$ErrorActionPreference = 'Stop'
$workflow = Get-Content -Raw (Join-Path $Root 'Source\FishingRodWorkflow.cs')
$rods = Get-Content -Raw (Join-Path $Root 'Source\FishingRods.cs')
$core = Get-Content -Raw (Join-Path $Root 'Source\AquacultureCore.cs')
$expertise = Get-Content -Raw (Join-Path $Root 'Source\FishingExpertise.cs')
$keyed = Get-Content -Raw (Join-Path $Root '1.6\Languages\English\Keyed\AquacultureFishing.xml')

$checks = [ordered]@{
    'workflow component persists sessions' = $workflow -match 'FishingRodWorkflowComponent' -and
        $workflow -match 'Scribe_Collections.Look\(ref sessions'
    'session persists rod and previous weapon' = $workflow -match 'Scribe_References.Look\(ref rod' -and
        $workflow -match 'Scribe_References.Look\(ref previousPrimary'
    'map index is not a per-tick map scan' = $workflow -match 'FishingRodMapComponent' -and
        $workflow -match 'nextCleanupTick' -and $workflow -notmatch 'listerThings\.AllThings'
    'inventory candidates are considered' = $workflow -match 'BestInventoryRod' -and
        $workflow -match 'GetDirectlyHeldThings\(\)\.OfType<ThingWithComps>'
    'only valid storage candidates are considered' = $workflow -match 'StoreUtility\.IsInValidStorage'
    'forbidden and reachable checks exist' = $workflow -match 'IsForbidden\(pawn\)' -and
        $workflow -match 'CanReserveAndReach'
    'upgraded rods are preferred' = $workflow -match 'OrderByDescending\(RodMass\)' -and
        $workflow -match 'ThenByDescending\(RodCatch\)' -and $workflow -match 'ThenBy\(RodTiming\)'
    'reservation is attached to the fishing job' = $workflow -match 'pawn\.Reserve\(session\.rod, job'
    'dependency reservation hook is installed' = $core -match 'FishingPreToilReservationsPrefix'
    'acquisition precedes fishing phases' = $expertise -match 'FishingRodWorkflow\.PreparationToils\(__instance\)' -and
        $expertise -match 'ReplaceFishingDelay\(__result'
    'temporary rod is equipped through standard tracker' = $workflow -match 'TryTransferEquipmentToContainer' -and
        $workflow -match 'AddEquipment\(rod\)'
    'previous primary is restored' = $workflow -match 'RestorePreviousWeapon' -and
        $workflow -match 'session\.previousPrimary'
    'cleanup handles every job condition' = $core -match 'AccessTools\.Method\(typeof\(JobDriver\), "Cleanup"\)' -and
        $workflow -match 'CleanupPostfix'
    'destruction does not destroy the selected rod' = $workflow -match 'PutInInventoryOrDrop' -and
        $workflow -notmatch 'DestroyEquipment\(rod\)'
    'clear rejection reasons are keyed' = $workflow -match 'JobFailReason\.Is' -and
        $keyed -match 'AquacultureFishing\.NoUsableFishingRod' -and
        $keyed -match 'AquacultureFishing\.NoReachableFishingRod'
    'manually equipped rods remain valid' = $rods -match 'EquippedRodThing' -and
        $workflow -match 'FishingRodSource\.Equipped'
    'prisoner and slave restrictions are explicit' = $workflow -match 'pawn\.IsPrisoner' -and
        $workflow -match 'pawn\.IsSlave'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count -gt 0) {
    throw ('Fishing rod workflow verification failed: ' + (($failed | ForEach-Object Key) -join ', '))
}

Write-Output ("Fishing rod workflow verification passed ({0} checks)." -f $checks.Count)
