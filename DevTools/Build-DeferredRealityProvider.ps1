[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$RimWorldDir = (Join-Path $PSScriptRoot '..\..\..'),
    [string]$HarmonyPath = 'C:\Games\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll',
    [string]$InsightCanvasDir = (Join-Path $PSScriptRoot '..\..\InsightCanvas'),
    [string]$KnowledgeFrameworkAssemblyPath = (Join-Path $PSScriptRoot '..\..\KnowledgeFramework\1.6\Assemblies\KnowledgeFramework.dll'),
    [string]$DeferredRealityAssembly = (Join-Path $PSScriptRoot '..\..\DeferredRealityFramework\1.6\Assemblies\DeferredRealityFramework.dll')
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'Source\DeferredRealityProvider\DeferredReality.Aquaculture.csproj'
$assembly = Join-Path $repositoryRoot '1.6\Assemblies\DeferredReality.Aquaculture.dll'
$manifest = Join-Path $repositoryRoot '1.6\Assemblies\DeferredReality.Aquaculture.manifest.json'

if (-not (Test-Path -LiteralPath $project)) { throw "Provider project not found: $project" }
if (-not (Test-Path -LiteralPath $HarmonyPath)) { throw "Harmony assembly not found: $HarmonyPath" }

$sourceCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$sourceDirty = @(& git -C $repositoryRoot status --porcelain --untracked-files=all).Count -gt 0

& dotnet build $project --configuration $Configuration `
    "-p:RimWorldDir=$RimWorldDir" `
    "-p:HarmonyPath=$HarmonyPath" `
    "-p:InsightCanvasDir=$InsightCanvasDir" `
    "-p:KnowledgeFrameworkAssemblyPath=$KnowledgeFrameworkAssemblyPath" `
    "-p:DeferredRealityAssembly=$DeferredRealityAssembly"
if ($LASTEXITCODE -ne 0) { throw 'Deferred Reality provider build failed.' }
if (-not (Test-Path -LiteralPath $assembly)) { throw "Provider output not found: $assembly" }

$hash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
$assemblyName = [Reflection.AssemblyName]::GetAssemblyName($assembly)
$manifestObject = [ordered]@{
    schema = 1
    providerId = 'lan.aquaculture.natural-water'
    assemblyName = $assemblyName.Name
    assemblyVersion = $assemblyName.Version.ToString()
    sourceCommit = $sourceCommit
    sourceDirty = $sourceDirty
    sha256 = $hash
    semanticApiVersion = 2
    providerSchemaVersion = 2
    deferredRealitySaveSchema = 5
    capabilities = @('Regions', 'Populations', 'Processes', 'Anchors', 'Diagnostics')
    operationRetentionTicks = -1
    compactableOperationKinds = @()
    exactlyOnceSequenceDomain = 'natural-water-population'
}

$manifestObject | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest -Encoding UTF8
"Provider: $assembly"
"SHA256: $hash"
"Manifest: $manifest"
