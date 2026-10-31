# Developer-only RimWorld 1.7 build-input inventory.
# Reads only explicit input paths. Does not build, launch, deploy or qualify.
[CmdletBinding()]
param(
    [string]$RimWorldRoot,
    [string]$HarmonyAssemblyPath,
    [string]$InsightCanvasAssemblyPath,
    [string]$KnowledgeFrameworkAssemblyPath,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-InputFile {
    param(
        [string]$Id,
        [string]$Path,
        [string]$FileName,
        [switch]$Versioned
    )
    $result = [ordered]@{
        id = $Id
        state = 'MISSING_PATH'
        bytes = $null
        sha256 = $null
    }
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return [pscustomobject]$result
    }
    try {
        if (-not [IO.Path]::IsPathRooted($Path)) {
            $result.state = 'INVALID_PATH'
            return [pscustomobject]$result
        }
        $leaf = [IO.Path]::GetFileName($Path.TrimEnd([char[]]@('\', '/')))
        if (-not [string]::Equals($leaf, $FileName, [StringComparison]::OrdinalIgnoreCase)) {
            $result.state = 'UNEXPECTED_FILENAME'
            return [pscustomobject]$result
        }
        if ($Versioned) {
            $suffix = '\1.7\Assemblies\' + $FileName
            $normalized = $Path.Replace('/', '\')
            if (-not $normalized.EndsWith($suffix, [StringComparison]::OrdinalIgnoreCase)) {
                $result.state = 'NOT_17_ASSEMBLY_PATH'
                return [pscustomobject]$result
            }
        }
        $before = Get-Item -LiteralPath $Path -ErrorAction Stop
        if ($before.PSIsContainer -or
            (($before.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            $result.state = 'NOT_REGULAR_FILE'
            return [pscustomobject]$result
        }
        if ($before.Length -le 0 -or $before.Length -gt 268435456) {
            $result.state = 'INVALID_FILE_LENGTH'
            return [pscustomobject]$result
        }
        $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash
        $after = Get-Item -LiteralPath $Path -ErrorAction Stop
        if ($after.PSIsContainer -or
            (($after.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) -or
            $after.Length -ne $before.Length -or
            $after.LastWriteTimeUtc -ne $before.LastWriteTimeUtc) {
            $result.state = 'CHANGED_DURING_HASH'
            return [pscustomobject]$result
        }
        if ($hash -notmatch '^[A-Fa-f0-9]{64}$') {
            $result.state = 'HASH_UNAVAILABLE'
            return [pscustomobject]$result
        }
        $result.state = 'PRESENT_HASHED_UNQUALIFIED'
        $result.bytes = [long]$after.Length
        $result.sha256 = $hash.ToLowerInvariant()
    }
    catch {
        # An exception can contain a private account-local path.
        $result.state = 'MISSING_OR_UNREADABLE'
    }
    return [pscustomobject]$result
}

if ($SelfTest) {
    $blank = Test-InputFile -Id 'selftest.blank' -Path '' -FileName 'Waylith.dll'
    $relative = Test-InputFile -Id 'selftest.relative' -Path 'Waylith.dll' -FileName 'Waylith.dll'
    $name = Test-InputFile -Id 'selftest.name' -Path 'C:\1.7\Assemblies\unexpected.dll' -FileName '0Harmony.dll' -Versioned
    $version = Test-InputFile -Id 'selftest.version' -Path 'C:\1.6\Assemblies\0Harmony.dll' -FileName '0Harmony.dll' -Versioned
    if ($blank.state -ne 'MISSING_PATH' -or $relative.state -ne 'INVALID_PATH' -or
        $name.state -ne 'UNEXPECTED_FILENAME' -or $version.state -ne 'NOT_17_ASSEMBLY_PATH') {
        throw 'Aquaculture 1.7 read-only input preflight negative self-test failed.'
    }
    Write-Output 'AQUACULTURE17_INPUT_SELFTEST_PASS (negative cases only; build/runtime NOT_RUN)'
    return
}

$root = if ([string]::IsNullOrWhiteSpace($RimWorldRoot)) { '' } else { $RimWorldRoot }
$inputs = @(
    Test-InputFile -Id 'game.assembly-csharp' -Path $(if ($root) { Join-Path $root 'Assembly-CSharp.dll' } else { '' }) -FileName 'Assembly-CSharp.dll'
    Test-InputFile -Id 'game.waylith' -Path $(if ($root) { Join-Path $root 'Waylith.dll' } else { '' }) -FileName 'Waylith.dll'
    Test-InputFile -Id 'game.unity-stubs' -Path $(if ($root) { Join-Path $root 'UnityStubs.dll' } else { '' }) -FileName 'UnityStubs.dll'
    Test-InputFile -Id 'framework.harmony' -Path $HarmonyAssemblyPath -FileName '0Harmony.dll' -Versioned
    Test-InputFile -Id 'framework.insight-canvas' -Path $InsightCanvasAssemblyPath -FileName 'InsightCanvas.dll' -Versioned
    Test-InputFile -Id 'framework.knowledge' -Path $KnowledgeFrameworkAssemblyPath -FileName 'KnowledgeFramework.dll' -Versioned
)
$blocked = @($inputs | Where-Object { $_.state -ne 'PRESENT_HASHED_UNQUALIFIED' })
$assessment = if ($blocked.Count -eq 0) { 'INPUT_FILES_PRESENT_UNQUALIFIED' } else { 'INPUTS_BLOCKED' }
[pscustomobject][ordered]@{
    schemaVersion = 'aquaculture17-build-inputs/v1'
    assessment = $assessment
    build = 'NOT_RUN'
    runtimeLoad = 'NOT_RUN'
    gameVersion = 'UNVERIFIED'
    dependencyCompatibility = 'UNVERIFIED'
    notes = 'Presence and SHA256 alone do not establish RimWorld 1.7, source freshness, or load compatibility. DRF and VFE are outside this file-only inventory.'
    inputs = $inputs
} | ConvertTo-Json -Depth 6
