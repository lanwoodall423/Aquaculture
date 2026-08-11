param(
    [string]$DevBridgeRoot = 'C:\Games\Steam\steamapps\common\RimWorld\Mods\DevBridge2',
    [Parameter(Mandatory = $true)][string]$Command,
    [string]$Argument = '',
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
if ($TimeoutSeconds -lt 1) { throw 'TimeoutSeconds must be positive.' }

$bridgeRoot = [IO.Path]::GetFullPath($DevBridgeRoot)
$bridge = Join-Path $bridgeRoot 'DevBridge.cmd'
$runtime = Join-Path $bridgeRoot 'Runtime'
if (-not (Test-Path -LiteralPath $bridge -PathType Leaf)) {
    throw "DevBridge.cmd was not found: $bridge"
}

function Invoke-DevBridge {
    param([string[]]$Arguments)
    $lines = @(& $bridge @Arguments 2>&1 | ForEach-Object { $_.ToString() })
    [pscustomobject]@{ Lines = $lines; Text = $lines -join [Environment]::NewLine; ExitCode = $LASTEXITCODE }
}

function Read-JsonFile {
    param([string]$Path)
    try {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
        Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch { return $null }
}

function Write-AtomicJson {
    param([string]$Path, [object]$Value)
    $temporary = "$Path.tmp-$([Guid]::NewGuid().ToString('N'))"
    try {
        [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json -Compress -Depth 4), [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporary -Destination $Path -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporary -PathType Leaf) { Remove-Item -LiteralPath $temporary -Force }
    }
}

function Get-BridgeIdentity {
    param([object]$StatusResult)
    $launch = [regex]::Match($StatusResult.Text, '(?im)^Launch ID:\s*(?<value>\S+)')
    $generation = [regex]::Match($StatusResult.Text, '(?im)^Generation:\s*(?<value>\d+)')
    if (-not $launch.Success -or -not $generation.Success) { throw "DevBridge status did not expose launch/generation: $($StatusResult.Text)" }
    [pscustomobject]@{ LaunchId = $launch.Groups['value'].Value; Generation = [int]$generation.Groups['value'].Value }
}

$lease = $null
$runId = [Guid]::NewGuid().ToString('N')
$requestPath = Join-Path $runtime "AquacultureFishing.DiagnosticRequest.$runId.json"
$resultPath = Join-Path $runtime "AquacultureFishing.DiagnosticResult.$runId.json"
try {
    $ready = Invoke-DevBridge @('wait-ready')
    $ready.Lines | Write-Output
    if ($ready.ExitCode -ne 0) { throw "DevBridge wait-ready failed: $($ready.Text)" }

    $status = Invoke-DevBridge @('status')
    $status.Lines | Write-Output
    if ($status.ExitCode -ne 0) { throw "DevBridge status failed: $($status.Text)" }
    $identity = Get-BridgeIdentity $status

    $begin = Invoke-DevBridge @('test', 'begin')
    $begin.Lines | Write-Output
    if ($begin.ExitCode -ne 0) { throw "DevBridge test begin failed: $($begin.Text)" }
    $leaseMatch = [regex]::Match($begin.Text, 'test end\s+(?<lease>[A-Za-z0-9_-]+)')
    if (-not $leaseMatch.Success) { throw "Could not identify the owned lease: $($begin.Text)" }
    $lease = $leaseMatch.Groups['lease'].Value

    $request = [ordered]@{
        schemaVersion = 1
        suite = 'aquaculture-diagnostic'
        runId = $runId
        launchId = $identity.LaunchId
        generation = $identity.Generation
        command = $Command
        argument = $Argument
        requestedUtc = [DateTime]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    }
    Write-AtomicJson -Path $requestPath -Value $request
    Write-Output "Requested mod-owned diagnostic $Command (run $runId)."

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $result = Read-JsonFile $resultPath
        if ($null -ne $result -and $result.suite -eq 'aquaculture-diagnostic' -and
            $result.runId -eq $runId -and $result.launchId -eq $identity.LaunchId -and
            [int]$result.generation -eq $identity.Generation) {
            $result | ConvertTo-Json -Depth 6 | Write-Output
            if ($result.status -ne 'PASS') { throw "Aquaculture diagnostic failed: $($result.error)" }
            return
        }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Timed out waiting for $resultPath" }
        Start-Sleep -Seconds 2
    } while ($true)
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($lease)) {
        $end = Invoke-DevBridge @('test', 'end', $lease)
        $end.Lines | Write-Output
        if ($end.ExitCode -ne 0) { throw "DevBridge test lease release failed for $lease." }
        $final = Invoke-DevBridge @('status')
        $final.Lines | Write-Output
        if ($final.ExitCode -ne 0 -or $final.Text -notmatch '(?im)^State:\s*READY') {
            throw "DevBridge did not finish READY after releasing lease $lease."
        }
    }
}
