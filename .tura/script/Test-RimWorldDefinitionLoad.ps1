param(
    [string]$DevBridgeRoot = 'C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2',
    [int]$TimeoutSeconds = 240,
    [switch]$SkipRestart
)

$ErrorActionPreference = 'Stop'
$bridge = Join-Path $DevBridgeRoot 'DevBridge.cmd'
if (-not (Test-Path -LiteralPath $bridge -PathType Leaf)) {
    throw "DevBridge2 coordinator not found: $bridge"
}

function Invoke-Bridge {
    param([string[]]$Arguments)
    $output = @(& $bridge @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw ('DevBridge2 {0} failed with exit code {1}: {2}' -f ($Arguments -join ' '), $LASTEXITCODE, ($output -join ' '))
    }
    return $output
}

$leaseId = $null
try {
    if (-not $SkipRestart) { Invoke-Bridge -Arguments @('restart') | Out-Null }
    Invoke-Bridge -Arguments @('wait-ready') | Out-Null
    $status = Invoke-Bridge -Arguments @('status')
    if (-not (($status -join "`n") -match '(?m)^State:\s*READY\s*$')) {
        throw ('DevBridge2 did not report READY: ' + ($status -join ' '))
    }

    $begin = Invoke-Bridge -Arguments @('test', 'begin')
    $beginText = $begin -join "`n"
    $leaseMatch = [regex]::Match($beginText, '(?im)lease(?:\s+acquired)?\s*[:=]\s*([A-Za-z0-9_-]+)')
    if (-not $leaseMatch.Success) { throw ('DevBridge2 test begin did not return an owned lease: ' + $beginText) }
    $leaseId = $leaseMatch.Groups[1].Value

    $log = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log'
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $ready = $false
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $log) {
            $lines = Get-Content -LiteralPath $log -ErrorAction SilentlyContinue
            if (($lines -join "`n") -match 'Unloading [0-9]+ unused Assets') { $ready = $true; break }
        }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw "DevBridge2-managed definition-load check timed out after $TimeoutSeconds seconds." }

    $lines = Get-Content -LiteralPath $log
    $aquacultureErrors = @($lines | Select-String -Pattern 'Could not resolve cross-reference:.*ThingDefCountClass|Config error in AF_|Exception.*AquacultureFishing|AquacultureFishing.*(?:error|exception)|TryMakePreToilReservations\(\) returned false.*curJob = Fish|Exception in WorkGiverOptionProvider_WorkGivers\.GetWorkGiversOptionsFor for AF_UpgradeFishingRod')
    if ($aquacultureErrors.Count -gt 0) {
        throw ('Fresh DevBridge2-managed RimWorld load contains Aquaculture Fishing errors:' + [Environment]::NewLine + (($aquacultureErrors | ForEach-Object Line) -join [Environment]::NewLine))
    }

    $allCrossReferences = @($lines | Select-String -Pattern 'Could not resolve cross-reference')
    Write-Output 'DevBridge2-managed RimWorld definition load reached startup completion.'
    Write-Output 'Aquaculture Fishing definition errors: 0'
    Write-Output "Unrelated unresolved cross-references: $($allCrossReferences.Count)"
    $allCrossReferences | ForEach-Object { Write-Output ('  ' + $_.Line) }
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($leaseId)) {
        & $bridge test end $leaseId | Out-Null
    }
}
