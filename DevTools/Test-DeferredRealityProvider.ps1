[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$assembly = Join-Path $RepositoryRoot '1.6\Assemblies\DeferredReality.Aquaculture.dll'
$manifestPath = Join-Path $RepositoryRoot '1.6\Assemblies\DeferredReality.Aquaculture.manifest.json'
if (-not (Test-Path -LiteralPath $assembly)) { throw "Provider DLL is missing: $assembly" }
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Provider manifest is missing: $manifestPath" }

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$hash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
if ($manifest.providerId -ne 'lan.aquaculture.natural-water') { throw 'Provider ID mismatch.' }
if ($manifest.sha256 -ne $hash) { throw 'Provider manifest hash does not match the DLL.' }
if ($manifest.semanticApiVersion -lt 2 -or $manifest.providerSchemaVersion -lt 2) { throw 'Provider contract version is too old.' }
if ($manifest.deferredRealitySaveSchema -lt 5) { throw 'Deferred Reality save schema is too old.' }
if ($manifest.capabilities.Count -lt 5) { throw 'Provider capability manifest is incomplete.' }
if ($manifest.operationRetentionTicks -ne -1) { throw 'Exactly-once markers must remain retained.' }
if ($manifest.exactlyOnceSequenceDomain -ne 'natural-water-population') { throw 'Sequence domain mismatch.' }

"Deferred Reality provider contract passed: $($manifest.providerId), SHA256 $hash"
