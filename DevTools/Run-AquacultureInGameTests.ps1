param(
    [string]$DevBridgeRoot = 'C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2',
    [int]$TimeoutSeconds = 600,
    [int]$Runs = 2,
    [switch]$SkipRestart
)

$ErrorActionPreference = 'Stop'
if ($Runs -lt 1) { throw 'Runs must be at least 1.' }

$bridgeRoot = [IO.Path]::GetFullPath($DevBridgeRoot)
$bridge = Join-Path $bridgeRoot 'DevBridge.cmd'
$runtime = Join-Path $bridgeRoot 'Runtime'
$baselineReport = Join-Path $runtime 'AquacultureFishing.InGameTests.json'

if (-not (Test-Path -LiteralPath $bridge -PathType Leaf)) {
    throw "DevBridge.cmd was not found: $bridge"
}

function Invoke-DevBridge {
    param([string[]]$Arguments)

    $lines = @(& $bridge @Arguments 2>&1 | ForEach-Object { $_.ToString() })
    [pscustomobject]@{
        Lines = $lines
        Text = $lines -join [Environment]::NewLine
        ExitCode = $LASTEXITCODE
    }
}

function Read-JsonFile {
    param([string]$Path)

    try {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
        return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
    }
    catch {
        return $null
    }
}

function Write-AtomicJson {
    param([string]$Path, [object]$Value)

    $temporary = "$Path.tmp-$([Guid]::NewGuid().ToString('N'))"
    $json = $Value | ConvertTo-Json -Compress -Depth 4
    try {
        [IO.File]::WriteAllText($temporary, $json, [Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            Move-Item -LiteralPath $temporary -Destination $Path -Force
        }
        else {
            Move-Item -LiteralPath $temporary -Destination $Path
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporary -PathType Leaf) {
            Remove-Item -LiteralPath $temporary -Force
        }
    }
}

function Get-BridgeIdentity {
    param([object]$StatusResult)

    $launchMatch = [regex]::Match($StatusResult.Text, '(?im)^Launch ID:\s*(?<launch>\S+)')
    $generationMatch = [regex]::Match($StatusResult.Text, '(?im)^Generation:\s*(?<generation>\d+)')
    $targetGenerationMatch = [regex]::Match($StatusResult.Text, '(?im)pending for generation\s+(?<generation>\d+)')
    if (-not $launchMatch.Success) {
        throw "DevBridge status did not expose the active launch ID: $($StatusResult.Text)"
    }
    [pscustomobject]@{
        LaunchId = $launchMatch.Groups['launch'].Value
        Generation = if ($targetGenerationMatch.Success) {
            [int]$targetGenerationMatch.Groups['generation'].Value
        }
        elseif ($generationMatch.Success) {
            [int]$generationMatch.Groups['generation'].Value
        }
        else { $null }
    }
}

function Wait-Result {
    param(
        [string]$Path,
        [string]$Suite,
        [string]$RunId,
        [string]$LaunchId,
        [Nullable[int]]$Generation,
        [int]$Timeout,
        [switch]$AllowFailure
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($Timeout)
    do {
        $result = Read-JsonFile $Path
        if ($null -ne $result) {
            $identityMatches = $result.launchId -eq $LaunchId -and
                ($null -eq $Generation -or [int]$result.generation -eq $Generation)
            $runMatches = ([string]::IsNullOrWhiteSpace($RunId) -or $result.runId -eq $RunId)
            if ($result.suite -ne $Suite -or -not $identityMatches -or -not $runMatches) {
                $result = $null
            }
        }
        if ($null -ne $result) {
            if ($result.status -eq 'PASS') { return $result }
            if ($result.status -eq 'FAIL') {
                if ($AllowFailure) { return $result }
                throw "Aquaculture mod-owned tests failed ($($result.failed) checks). See $Path"
            }
        }
        if ([DateTime]::UtcNow -ge $deadline) {
            throw "Timed out waiting for the mod-owned result: $Path"
        }
        Start-Sleep -Seconds 2
    } while ($true)
}

Write-Output 'Starting DevBridge2-coordinated Aquaculture in-game run.'
$restartProcess = $null
if ($SkipRestart) {
    Write-Output 'DevBridge2 lifecycle: wait-ready'
    $lifecycleResult = Invoke-DevBridge @('wait-ready')
    $lifecycleResult.Lines | Write-Output
    if ($lifecycleResult.ExitCode -ne 0) {
        throw "DevBridge wait-ready failed: $($lifecycleResult.Text)"
    }
}
else {
    # DevBridge restart normally waits for the new map. Dispatch it in a hidden
    # coordinator process so this harness can acquire its lease and queue the
    # mod-owned requests while the new game process is still loading.
    Write-Output 'DevBridge2 lifecycle: restart (coordinated background dispatch)'
    $restartProcess = Start-Process -FilePath $bridge -ArgumentList @('restart') -WindowStyle Hidden -PassThru
    $restartDeadline = [DateTime]::UtcNow.AddSeconds(60)
    $restartObserved = $false
    do {
        $probe = Invoke-DevBridge @('status')
        if ($probe.ExitCode -eq 0 -and
            $probe.Text -match '(?im)^State:\s*LOADING' -and
            $probe.Text -match '(?im)Restart:\s*pending for generation\s+\d+') {
            $restartObserved = $true
            break
        }
        if ([DateTime]::UtcNow -ge $restartDeadline) { break }
        Start-Sleep -Milliseconds 500
    } while ($true)
    if (-not $restartObserved) {
        throw 'DevBridge restart did not enter its coordinated loading phase.'
    }
    Write-Output 'DevBridge restart is loading; queueing requests before readiness.'
}

$lease = $null
try {
    $statusResult = Invoke-DevBridge @('status')
    $statusResult.Lines | Write-Output
    if ($statusResult.ExitCode -ne 0) {
        throw "DevBridge status failed after lifecycle dispatch: $($statusResult.Text)"
    }
    $identity = Get-BridgeIdentity $statusResult

    # Keep a valid report from this exact launch. This avoids deleting a report that
    # the mod completed between lifecycle readiness and lease/status acquisition.
    $existing = Read-JsonFile $baselineReport
    if ($null -ne $existing -and
        ($existing.launchId -ne $identity.LaunchId -or
            ($null -ne $identity.Generation -and [int]$existing.generation -ne $identity.Generation))) {
        Remove-Item -LiteralPath $baselineReport -Force
    }

    $runPlans = @()
    for ($runIndex = 1; $runIndex -le $Runs; $runIndex++) {
        $runId = [Guid]::NewGuid().ToString('N')
        $requestPath = Join-Path $runtime "AquacultureFishing.InGameTestRequest.$runId.json"
        $resultPath = Join-Path $runtime "AquacultureFishing.InGameTestResult.$runId.json"
        $request = [ordered]@{
            schemaVersion = 1
            suite = 'inhabited-pond-golden-path'
            runId = $runId
            launchId = $identity.LaunchId
            generation = $identity.Generation
            requestedUtc = [DateTime]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
        }
        $runPlans += [pscustomobject]@{
            Index = $runIndex
            RunId = $runId
            RequestPath = $requestPath
            ResultPath = $resultPath
            Request = $request
        }
    }

    # On a fresh restart quicktest can be paused immediately after the baseline
    # settles.  Queue requests before waiting for readiness so the mod can consume
    # them during map initialization on its own game thread.
    if (-not $SkipRestart) {
        foreach ($plan in $runPlans) {
            Write-AtomicJson -Path $plan.RequestPath -Value $plan.Request
            Write-Output "Queued mod-owned inhabited-pond golden path $($plan.Index)/$Runs (run $($plan.RunId))."
        }
        $readyResult = Invoke-DevBridge @('wait-ready')
        $readyResult.Lines | Write-Output
        if ($readyResult.ExitCode -ne 0) {
            throw "DevBridge wait-ready failed: $($readyResult.Text)"
        }
        if ($null -ne $restartProcess) {
            $restartProcess.WaitForExit(10000) | Out-Null
        }
    }

    # DevBridge intentionally holds test begin while a restart is pending. The
    # requests above are queued without a lease, then this exact lease is acquired
    # once the coordinator reports READY and before result inspection begins.
    $beginResult = Invoke-DevBridge @('test', 'begin')
    $beginResult.Lines | Write-Output
    if ($beginResult.ExitCode -ne 0) {
        throw "DevBridge test lease acquisition failed: $($beginResult.Text)"
    }
    $leaseMatch = [regex]::Match($beginResult.Text, 'test end\s+(?<lease>[A-Za-z0-9_-]+)')
    if (-not $leaseMatch.Success) {
        throw "DevBridge test begin did not print a release command. Output: $($beginResult.Text)"
    }
    $lease = $leaseMatch.Groups['lease'].Value

    $readyStatus = Invoke-DevBridge @('status')
    $readyStatus.Lines | Write-Output
    if ($readyStatus.ExitCode -ne 0) {
        throw "DevBridge status failed after lease acquisition: $($readyStatus.Text)"
    }
    $readyIdentity = Get-BridgeIdentity $readyStatus
    if ($readyIdentity.LaunchId -ne $identity.LaunchId -or $readyIdentity.Generation -ne $identity.Generation) {
        $identity = $readyIdentity
        foreach ($plan in $runPlans) {
            $plan.Request.launchId = $identity.LaunchId
            $plan.Request.generation = $identity.Generation
            if (-not $SkipRestart) {
                Write-AtomicJson -Path $plan.RequestPath -Value $plan.Request
            }
        }
    }

    $baseline = Wait-Result -Path $baselineReport -Suite 'aquaculture-mod-owned-live-world' -RunId '' `
        -LaunchId $identity.LaunchId -Generation $identity.Generation -Timeout $TimeoutSeconds -AllowFailure
    if ($baseline.status -eq 'PASS') {
        Write-Output "Baseline mod-owned checks passed: $($baseline.passed) (launch $($identity.LaunchId), generation $($identity.Generation))."
    }
    else {
        Write-Output "Baseline mod-owned checks reported $($baseline.failed) failure(s); continuing to collect the explicitly requested golden-path runs."
    }

    if ($SkipRestart) {
        foreach ($plan in $runPlans) {
            Write-AtomicJson -Path $plan.RequestPath -Value $plan.Request
            Write-Output "Requested mod-owned inhabited-pond golden path $($plan.Index)/$Runs (run $($plan.RunId))."
        }
    }

    $runSummaries = @()
    $goldenFailure = $false
    foreach ($plan in $runPlans) {
        $runId = $plan.RunId
        $resultPath = $plan.ResultPath
        $golden = Wait-Result -Path $resultPath -Suite 'inhabited-pond-golden-path' -RunId $runId `
            -LaunchId $identity.LaunchId -Generation $identity.Generation -Timeout $TimeoutSeconds -AllowFailure
        if ($golden.status -ne 'PASS') { $goldenFailure = $true }
        $verb = if ($golden.status -eq 'PASS') { 'passed' } else { 'reported failures' }
        Write-Output "Golden-path run $($plan.Index) ${verb}: $($golden.passed) checks; $($golden.tests.Count) total."
        $runSummaries += [pscustomobject]@{
            RunId = $runId
            Status = $golden.status
            Passed = [int]$golden.passed
            Failed = [int]$golden.failed
            ResultPath = $resultPath
        }
    }

    $runSummaries | Format-Table -AutoSize | Out-String | Write-Output
    if ($baseline.status -ne 'PASS') {
        throw "Aquaculture baseline checks failed ($($baseline.failed) checks). See $baselineReport"
    }
    if ($goldenFailure) {
        throw "Aquaculture inhabited-pond golden-path checks failed. See the per-run result paths above."
    }
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($lease)) {
        $endResult = Invoke-DevBridge @('test', 'end', $lease)
        $endResult.Lines | Write-Output
        if ($endResult.ExitCode -ne 0) {
            throw "DevBridge test lease release failed for $lease."
        }
        $finalStatus = Invoke-DevBridge @('status')
        $finalStatus.Lines | Write-Output
        if ($finalStatus.ExitCode -ne 0 -or $finalStatus.Text -notmatch '(?i)READY') {
            throw "DevBridge did not finish READY after releasing lease $lease."
        }
    }
}
