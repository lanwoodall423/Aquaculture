param(
    [string]$Game = 'C:\Games\Steam\steamapps\common\RimWorld\RimWorldWin64.exe',
    [int]$TimeoutSeconds = 240
)

$ErrorActionPreference = 'Stop'
$log = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log'
$started = Get-Date
$process = Start-Process -FilePath $Game -WorkingDirectory (Split-Path -Parent $Game) -WindowStyle Minimized -PassThru
try {
    $deadline = $started.AddSeconds($TimeoutSeconds)
    $ready = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        $process.Refresh()
        if ($process.HasExited) {
            $tail = if (Test-Path $log) { (Get-Content $log -Tail 60) -join [Environment]::NewLine } else { 'Player.log not found.' }
            throw "RimWorld exited before definition loading completed (exit $($process.ExitCode)).`n$tail"
        }
        if (Test-Path $log) {
            $logFile = Get-Item $log
            if ($logFile.LastWriteTime -ge $started -and (Select-String -Path $log -Pattern 'Unloading [0-9]+ unused Assets' -Quiet)) {
                $ready = $true
                break
            }
        }
    }
    if (-not $ready) {
        $tail = if (Test-Path $log) { (Get-Content $log -Tail 60) -join [Environment]::NewLine } else { 'Player.log not found.' }
        throw "RimWorld definition-load check timed out after $TimeoutSeconds seconds.`n$tail"
    }

    Start-Sleep -Seconds 5
    $lines = Get-Content $log
    $aquacultureErrors = @($lines | Select-String -Pattern 'Could not resolve cross-reference:.*ThingDefCountClass|Config error in AF_|Exception.*AquacultureFishing|AquacultureFishing.*(?:error|exception)|TryMakePreToilReservations\(\) returned false.*curJob = Fish|Exception in WorkGiverOptionProvider_WorkGivers\.GetWorkGiversOptionsFor for AF_UpgradeFishingRod')
    if ($aquacultureErrors.Count -gt 0) {
        throw ('Fresh RimWorld load contains Aquaculture Fishing errors:' + [Environment]::NewLine + (($aquacultureErrors | ForEach-Object Line) -join [Environment]::NewLine))
    }

    $allCrossReferences = @($lines | Select-String -Pattern 'Could not resolve cross-reference')
    Write-Output 'Fresh RimWorld definition load reached startup completion.'
    Write-Output 'Aquaculture Fishing definition errors: 0'
    Write-Output "Unrelated unresolved cross-references: $($allCrossReferences.Count)"
    $allCrossReferences | ForEach-Object { Write-Output ('  ' + $_.Line) }
}
finally {
    $process.Refresh()
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(10000) | Out-Null
    }
}
