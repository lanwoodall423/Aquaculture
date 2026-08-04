param([string]$Root = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))

$ErrorActionPreference = 'Stop'
$rods = Get-Content -Raw (Join-Path $Root 'Source\FishingRods.cs')
$core = Get-Content -Raw (Join-Path $Root 'Source\AquacultureCore.cs')
$workflow = Get-Content -Raw (Join-Path $Root 'Source\FishingRodWorkflow.cs')

$checks = [ordered]@{
    'pending part null guard' = $rods -match 'PendingPart\s*=>\s*pendingPart\.NullOrEmpty\(\)\s*\?\s*null\s*:'
    'installed part null guard' = $rods -match 'key\.NullOrEmpty\(\)\s*\?\s*null\s*:\s*DefDatabase<FishingTacklePartDef>\.GetNamedSilentFail\(key\)'
    'Odyssey non-scan job gate' = $core -match 'AccessTools\.Method\(odysseyWorkGiver, "NonScanJob"\)' -and
        $core -match 'FishingJobPostfix'
    'VFE job producer gate' = $core -match 'AccessTools\.Method\(workGiverType, "JobOnCell"\)' -and
        $core -match 'FishingJobPostfix'
    'rod reservation runs before dependency reservations' = $core -match 'TryMakePreToilReservations' -and
        $core -match 'FishingPreToilReservationsPrefix' -and $workflow -match 'pawn\.Reserve\(session\.rod'
    'existing reservations remain dependency-owned' = $workflow -notmatch 'StartJob\(' -and $workflow -notmatch 'ReservationPostfix'
    'workflow cleanup is idempotent' = $workflow -match 'component\.Remove\(session\)' -and $workflow -match 'RestoreTemporaryEquipment'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count -gt 0) {
    throw ('Fishing job guard regression failed: ' + (($failed | ForEach-Object Key) -join ', '))
}

Write-Output ("Fishing job guard regression passed ({0} checks)." -f $checks.Count)
