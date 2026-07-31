param([string]$Root = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
$required = @(
    'About\FISHING_ROD_ARCHITECTURE.md',
    'Source\FishingRods.cs',
    '1.6\Defs\FishingTackleDefs.xml',
    '1.6\Defs\FishingRodDefs.xml'
)
foreach ($relative in $required) {
    $path = Join-Path $Root $relative
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing fishing rod artifact: $relative" }
}

$rods = Get-Content -Raw (Join-Path $Root 'Source\FishingRods.cs')
$expertise = Get-Content -Raw (Join-Path $Root 'Source\FishingExpertise.cs')
$core = Get-Content -Raw (Join-Path $Root 'Source\AquacultureCore.cs')
$parts = Get-Content -Raw (Join-Path $Root '1.6\Defs\FishingTackleDefs.xml')
$rodDefs = Get-Content -Raw (Join-Path $Root '1.6\Defs\FishingRodDefs.xml')
[xml]$partsXml = $parts
$costNodes = @($partsXml.SelectNodes('//costs/*'))
$costShapeValid = $costNodes.Count -gt 0 -and @($costNodes | Where-Object {
    $_.Name -eq 'li' -or $_.Name -notmatch '^[A-Za-z0-9_]+$' -or
    $_.ChildNodes.Count -ne 1 -or $_.FirstChild.NodeType -ne [System.Xml.XmlNodeType]::Text -or
    $_.InnerText -notmatch '^\s*[1-9][0-9]*\s*$'
}).Count -eq 0
$rimWorldData = Join-Path (Split-Path -Parent (Split-Path -Parent $Root)) 'Data'
$referencedCostsExist = Test-Path -LiteralPath $rimWorldData
if ($referencedCostsExist) {
    $knownThingDefs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    Get-ChildItem $rimWorldData -Recurse -File -Filter *.xml | ForEach-Object {
        foreach ($match in [regex]::Matches((Get-Content -Raw $_.FullName), '<defName>\s*([^<]+?)\s*</defName>')) {
            [void]$knownThingDefs.Add($match.Groups[1].Value)
        }
    }
    $referencedCostsExist = @($costNodes | Where-Object {
        -not $knownThingDefs.Contains($_.Name)
    }).Count -eq 0
}

$checks = [ordered]@{
    'five tackle slots' = $rods -match 'Handle[\s\S]*Rod[\s\S]*Reel[\s\S]*Line[\s\S]*Lure'
    'equipped rod requirement' = $rods -match 'EquippedRod' -and $rods -match 'HasEquippedRod'
    'work giver rod gate' = $rods -match 'FishingJobPostfix' -and $core -match 'AccessTools.Method\(odysseyWorkGiver, "NonScanJob"\)'
    'reservation result retained' = $core -notmatch 'FishingReservationPostfix|OdysseyFishingReservationPostfix'
    'null-safe tackle def lookup' = $rods -match 'PendingPart => pendingPart.NullOrEmpty\(\)' -and $rods -match 'key.NullOrEmpty\(\)'
    'visible cast wait reel phases' = $rods -match 'Fishing - Cast' -and $rods -match 'Fishing - Wait' -and $rods -match 'Fishing - Reel'
    'phase duration split' = $rods -match 'CastTicks' -and $rods -match 'WaitTicks' -and $rods -match 'ReelTicks'
    'bite selected during wait' = $rods -match 'BeginWaitPhase' -and $expertise -match 'Pair\('
    'lure attraction weights' = $rods -match 'AttractionFor' -and $expertise -match 'RandomElementByWeight'
    'rod mass escape' = $expertise -match 'HookedFishMass' -and $expertise -match 'MaxFishMass'
    'line catch modifier' = $expertise -match 'CatchChanceFactor'
    'instance part persistence' = $rods -match 'Scribe_Values.Look\(ref handlePart' -and $rods -match 'Scribe_Values.Look\(ref lurePart'
    'pending upgrade persistence' = $rods -match 'pendingSlot' -and $rods -match 'pendingPart'
    'duplicate upgrade prevention' = $rods -match 'HasPendingUpgrade' -and $rods -match 'already has a pending tackle upgrade'
    'crafting upgrade job' = $rods -match 'JobDriver_UpgradeFishingRod' -and $rods -match 'WorkGiver_UpgradeFishingRod'
    'ingredients consumed' = $rods -match 'ConsumeIngredients'
    'tackle gizmo and window' = $rods -match 'EquipmentGizmosPostfix' -and $rods -match 'Dialog_FishingTackle'
    'inspect and stats surface' = $rods -match 'CompInspectStringExtra' -and $rods -match 'SpecialDisplayStats'
    'handle recreation applied' = $rods -match 'RecreationLossFactor' -and $rods -match 'needs\.joy\.CurLevel'
    'material and work cost visible' = $rods -match 'CostSummary' -and $rods -match 'Work " \+ Mathf.RoundToInt\(workAmount\)'
    'all named parts' = $parts -match 'Wrapped Leather Grip' -and $parts -match 'Carbon Fiber' -and $parts -match 'Glitterworld Smartreel' -and $parts -match 'Plasteel Wire' -and $parts -match 'Glitterworld Smart Lure'
    'def-driven cost work research' = $parts -match '<costs>' -and $parts -match '<workAmount>' -and $parts -match '<researchPrerequisite>'
    'ThingDefCountClass XML shape' = $costShapeValid
    'cost ThingDefs exist' = $referencedCostsExist
    'default rod' = $rodDefs -match 'AF_FishingRod' -and $rodDefs -match 'CompProperties_FishingRodTackle'
    'equippable rod has a tool' = $rodDefs -match '<tools>' -and $rodDefs -match '<li>Blunt</li>'
    'legacy save compatibility' = $expertise -match 'aquacultureFishingProgression' -and $expertise -match 'aquacultureFishingAttempts'
    'both framework hooks retained' = $core -match 'VCE_Fishing.JobDriver_Fish' -and $core -match 'RimWorld.JobDriver_Fish'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count -gt 0) { throw ('Fishing rod verification failed: ' + (($failed | ForEach-Object Key) -join ', ')) }
Write-Output ("Fishing rod verification passed ({0} checks)." -f $checks.Count)
