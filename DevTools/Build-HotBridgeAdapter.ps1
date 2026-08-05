param(
    [string]$Destination = (Join-Path $PSScriptRoot 'BridgeAdapters'),
    [string]$PublisherPath = (Join-Path $PSScriptRoot '..\..\RimWorldDevBridge\DevTools\Publish-RimWorldBridgeAdapter.ps1')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'BridgeAdapter\AquacultureFishing.BridgeAdapter.csproj'
$build = Join-Path $PSScriptRoot 'BridgeAdapter\Build'
$destination = [IO.Path]::GetFullPath($Destination)
$publisher = [IO.Path]::GetFullPath($PublisherPath)
$source = Join-Path $PSScriptRoot 'BridgeAdapter\AquacultureBridgeAdapter.cs'
$stamp = Get-Date -Format 'yyyyMMddHHmmssfff'
$assemblyName = "AquacultureFishing.BridgeAdapter.$stamp"

New-Item -ItemType Directory -Force -Path $build | Out-Null
dotnet build $project -c Release "-p:AssemblyName=$assemblyName" "-p:OutputPath=$build"
if ($LASTEXITCODE -ne 0) { throw "Hot adapter build failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $publisher -PathType Leaf)) { throw "Bridge adapter publisher not found: $publisher" }

$built = Join-Path $build ($assemblyName + '.dll')
$text = [IO.File]::ReadAllText($source)
$specs = @([regex]::Matches($text, '"(?<spec>[A-Z][A-Z0-9_]*\|[RW]\|[^"\r\n]+)"') |
    ForEach-Object { $_.Groups['spec'].Value })
# Aquaculture owns one deployable generation. Remove stale generations before
# publishing so the runtime cannot load an older adapter beside the new one.
Get-ChildItem -LiteralPath $destination -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^AquacultureFishing(\.BridgeAdapter)?\..+\.(dll|manifest\.json)$' } |
    Remove-Item -Force
& $publisher -AssemblyPath $built -Destination $destination -AdapterId 'AquacultureFishing' `
    -DisplayName 'Aquaculture Fishing' -Version '1.6.0' -Generation $stamp `
    -ProviderType 'AquacultureFishing.AquacultureBridgeAdapter' -CommandSpecs $specs `
    -RequiredPackageIds @('lan.aquaculture.fishing') -NoMapCommands @('AQUA_ADAPTER_STATUS') `
    -UiOnlyCommands @('AQUA_OPEN_PLANNER') `
    -ChangeSummary 'Natural food webs, habitat, journal, breed, ecology, performance, and validation diagnostics.'
if ($LASTEXITCODE -ne 0) { throw "Hot adapter publication failed with exit code $LASTEXITCODE." }
