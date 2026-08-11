param(
    [switch]$BuildHistoricalSource
)

$ErrorActionPreference = 'Stop'
Write-Output 'adapterBuild=NOT RUN coordinator=DevBridge2 reason=current-DevBridge2-has-no-adapter-registration-protocol'
Write-Output 'supportedDiagnostics=Source/AquacultureInGameTests.cs-plus-Run-AquacultureInGameTests.ps1'

if ($BuildHistoricalSource) {
    $project = Join-Path $PSScriptRoot 'BridgeAdapter\AquacultureFishing.BridgeAdapter.csproj'
    $build = Join-Path $PSScriptRoot 'BridgeAdapter\Build'
    New-Item -ItemType Directory -Force -Path $build | Out-Null
    dotnet build $project -c Release "-p:OutputPath=$build"
    if ($LASTEXITCODE -ne 0) { throw "Historical adapter source build failed with exit code $LASTEXITCODE." }
    Write-Output ('historicalAdapterBuild=PASS output={0}' -f (Join-Path $build 'AquacultureFishing.BridgeAdapter.dll'))
    Write-Output 'historicalAdapterRegistration=NOT RUN DevBridge2-does-not-consume-standalone-adapters'
}
