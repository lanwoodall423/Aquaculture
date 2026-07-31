param([string]$Root = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))

$ErrorActionPreference = 'Stop'
$rods = Get-Content -Raw (Join-Path $Root 'Source\FishingRods.cs')
$core = Get-Content -Raw (Join-Path $Root 'Source\AquacultureCore.cs')

$checks = [ordered]@{
    'pending part null guard' = $rods -match 'PendingPart\s*=>\s*pendingPart\.NullOrEmpty\(\)\s*\?\s*null\s*:'
    'installed part null guard' = $rods -match 'key\.NullOrEmpty\(\)\s*\?\s*null\s*:\s*DefDatabase<FishingTacklePartDef>\.GetNamedSilentFail\(key\)'
    'Odyssey non-scan job gate' = $core -match 'AccessTools\.Method\(odysseyWorkGiver, "NonScanJob"\)' -and
        $core -match 'FishingJobPostfix'
    'VFE job producer gate' = $core -match 'AccessTools\.Method\(workGiverType, "JobOnCell"\)' -and
        $core -match 'FishingJobPostfix'
    'reservation result is not changed' = $core -notmatch 'FishingReservationPostfix|OdysseyFishingReservationPostfix'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count -gt 0) {
    throw ('Fishing job guard regression failed: ' + (($failed | ForEach-Object Key) -join ', '))
}

Write-Output ("Fishing job guard regression passed ({0} checks)." -f $checks.Count)
