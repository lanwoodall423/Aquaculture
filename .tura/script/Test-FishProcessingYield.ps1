param([string]$Root = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))

$ErrorActionPreference = 'Stop'
$lifecycle = Get-Content -Raw (Join-Path $Root 'Source\FishLifeCycle.cs')
$core = Get-Content -Raw (Join-Path $Root 'Source\AquacultureCore.cs')
$traits = Get-Content -Raw (Join-Path $Root 'Source\FishTraits.cs')
$traitsTab = Get-Content -Raw (Join-Path $Root 'Source\FishTraitsTab.cs')
$journal = Get-Content -Raw (Join-Path $Root 'Source\AquacultureJournal.cs')
$recipe = Get-Content -Raw (Join-Path $Root '1.6\Defs\RecipeDefs.xml')
$keyed = Get-Content -Raw (Join-Path $Root '1.6\Languages\English\Keyed\AquacultureFishing.xml')
$guide = Get-Content -Raw (Join-Path $Root 'About\CONTENT_EXTENSION_GUIDE.md')
$testPlan = Get-Content -Raw (Join-Path $Root 'About\TEST_PLAN.md')

$minimumSize = 0.25
$maximumSize = 2.0
$minimumYield = 0.60
$maximumYield = 1.50
$exponent = 0.75
$baseMeat = 5

function Get-SizeYieldFactor([double]$size) {
    if ([double]::IsNaN($size) -or [double]::IsInfinity($size)) { $size = 1.0 }
    $bounded = [Math]::Max($minimumSize, [Math]::Min($maximumSize, $size))
    return [Math]::Max($minimumYield, [Math]::Min($maximumYield, [Math]::Pow($bounded, $exponent)))
}

function Get-ExpectedCount([double]$size) {
    return [Math]::Max(1, [int][Math]::Round($baseMeat * (Get-SizeYieldFactor $size), [MidpointRounding]::ToEven))
}

$checks = [ordered]@{
    'bounded pure size-yield helper exists' = $lifecycle -match 'class FishProcessingYield' -and
        $lifecycle -match 'MinimumSizeYieldFactor' -and $lifecycle -match 'MaximumSizeYieldFactor'
    'size curve does not reuse cubic mass' = $lifecycle -match 'Mathf\.Pow\(boundedSize, SizeCurveExponent\)' -and
        $lifecycle -notmatch 'SizeYieldFactor\([^\n]*MassFactor'
    'species/body yield is applied separately once' = $lifecycle -match 'TotalYieldFactor\(CompFishTraits fish\)' -and
        $lifecycle -match 'fish\.MeatYield' -and $lifecycle -match 'SizeYieldFactor\(fish\.SizeFactor\)'
    'recipe base count comes from the owned Def' = $lifecycle -match 'BaseMeatCount' -and
        $lifecycle -match 'recipe\?\.products' -and $recipe -match '<AF_FishMeat>5</AF_FishMeat>'
    'only supported fish processing is scaled' = $lifecycle -match 'ProcessRecipeDefName' -and
        $lifecycle -match 'FishUtility\.IsRuntimeFish' -and $lifecycle -match 'MeatDefName'
    'deterministic integer rounding is explicit' = $lifecycle -match 'Mathf\.RoundToInt\(BaseMeatCount\(\) \* TotalYieldFactor\(fish\)\)'
    'dead-fish inspection exposes expected count' = $core -match 'ExpectedProcessingYield' -and
        $keyed -match 'AquacultureFishing\.ExpectedProcessingYield'
    'trait and tab explanations expose size yield' = $traits -match 'ProcessingYieldText' -and
        $traitsTab -match 'Expected processing'
    'journal explanation is derived without new state' = $journal -match 'SizeYieldFactor\(record\.largestSize\)'
    'formula documentation is present' = $guide -match 'SizeYieldFactor' -and
        $guide -match 'does not use `MassFactor`' -and $testPlan -match 'outliers below 0\.25 or above 2\.0'
}

$cases = @(
    [pscustomobject]@{ Name = 'Minimum'; Size = 0.00 },
    [pscustomobject]@{ Name = 'Normal'; Size = 1.00 },
    [pscustomobject]@{ Name = '+25%'; Size = 1.25 },
    [pscustomobject]@{ Name = 'Very large'; Size = 2.00 },
    [pscustomobject]@{ Name = 'Small outlier'; Size = 0.05 },
    [pscustomobject]@{ Name = 'Large outlier'; Size = 8.00 }
)

$expected = @{
    'Minimum' = 3
    'Normal' = 5
    '+25%' = 6
    'Very large' = 8
    'Small outlier' = 3
    'Large outlier' = 8
}

$rows = foreach ($case in $cases) {
    $bounded = [Math]::Max($minimumSize, [Math]::Min($maximumSize, $case.Size))
    $factor = Get-SizeYieldFactor $case.Size
    $count = Get-ExpectedCount $case.Size
    $checks[($case.Name + ' deterministic yield')] = $count -eq $expected[$case.Name]
    [pscustomobject]@{
        Case = $case.Name
        SizeFactor = $case.Size
        BoundedSize = [Math]::Round($bounded, 3)
        SizeYield = [Math]::Round($factor, 3)
        FishMeat = $count
    }
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count -gt 0) {
    throw ('Fish processing yield verification failed: ' + (($failed | ForEach-Object Key) -join ', '))
}

Write-Output ("Fish processing yield verification passed ({0} checks)." -f $checks.Count)
$rows | Format-Table -AutoSize | Out-String | Write-Output
