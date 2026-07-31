param([string]$Root = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))

$ErrorActionPreference = 'Stop'
$rods = Get-Content -Raw (Join-Path $Root 'Source\FishingRods.cs')
$expertise = Get-Content -Raw (Join-Path $Root 'Source\FishingExpertise.cs')
$core = Get-Content -Raw (Join-Path $Root 'Source\AquacultureCore.cs')
$life = Get-Content -Raw (Join-Path $Root 'Source\FishLifeCycle.cs')
$rodDefs = Get-Content -Raw (Join-Path $Root '1.6\Defs\FishingRodDefs.xml')
$parts = Get-Content -Raw (Join-Path $Root '1.6\Defs\FishingTackleDefs.xml')
$rodTexture = Join-Path $Root '1.6\Textures\Things\Item\Equipment\FishingRod.png'

$rodPartNodes = ([xml]$parts).SelectNodes("//AquacultureFishing.FishingTacklePartDef[slot='Rod']")
$linePartNodes = ([xml]$parts).SelectNodes("//AquacultureFishing.FishingTacklePartDef[slot='Line' and not(isDefault='true')]")
$checks = [ordered]@{
    'rod texture copied and referenced' = (Test-Path -LiteralPath $rodTexture) -and $rodDefs -match '<texPath>Things/Item/Equipment/FishingRod</texPath>'
    'window renamed Tackle' = $rods -match 'Widgets\.Label\([^\r\n]+, "Tackle"\)'
    'readable effect labels' = $rods -match '"Reel Time"' -and $rods -match '"Max Fish Mass"' -and $rods -match 'FormatFactor'
    'aligned tackle columns' = $rods -match 'DrawColumnHeader' -and $rods -match 'nameRect' -and $rods -match 'effectsRect' -and $rods -match 'costRect'
    'cost column clears actions' = $rods -match 'costRect = new Rect\(row\.x \+ 598f, row\.y \+ 8f, 156f' -and
        $rods -match 'row\.xMax - 112f'
    'selection options use measured multiline labels' = $rods -match 'part\.LabelCap \+ "\\n" \+ part\.EffectsSummary \+ "\\n" \+ part\.CostSummary'
    'rod slot never changes reel time' = @($rodPartNodes | Where-Object { $_.reelTimeFactor }).Count -eq 0
    'rod and line change max mass' = @($rodPartNodes | Where-Object { -not $_.isDefault -and $_.maximumFishMassOffset }).Count -gt 0 -and
        @($linePartNodes | Where-Object { $_.maximumFishMassOffset }).Count -eq $linePartNodes.Count
    'default lure is None' = $parts -match '<defName>AF_Lure_Freshwater</defName><label>None</label><slot>Lure</slot><isDefault>true</isDefault>'
    'actual fish mass uses size traits' = $life -match 'stat == StatDefOf\.Mass' -and $life -match 'fish\.MassFactor' -and
        $core -match 'new StatModifier \{ stat = StatDefOf\.Mass, value = 0\.1f \}'
    'attempt stores hooked mass' = $expertise -match 'hookedFishMass' -and $expertise -match 'Scribe_Values\.Look\(ref hookedFishMass'
    'VFE transfer always clears' = $expertise -match 'VceCatchFinalizer' -and $expertise -match 'CaughtFishTraitTransfer\.Clear\(\)'
    'over-mass catch fails' = $expertise -match 'attempt\.HookedFishMass <= rod\.MaxFishMass'
    'pending rods are event indexed' = $rods -match 'PendingFishingRodRegistry' -and $rods -match 'PotentialWorkThingsGlobal'
    'scan validation has no material search' = $rods -match 'HasJobOnThing[\s\S]*?CanCheckMaterials' -and
        $rods -notmatch 'HasJobOnThing[\s\S]*?FindIngredients\(pawn, part\)[\s\S]*?public override Job JobOnThing'
    'material search failure is cached' = $rods -match 'DeferMaterialCheck' -and $rods -match 'MaterialRetryTicks'
    'duplicate state remains guarded' = $rods -match 'if \(HasPendingUpgrade\)' -and $rods -match 'pawn\.CanReserve\(thing\)'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count -gt 0) { throw ('Fishing rod revision regression failed: ' + (($failed | ForEach-Object Key) -join ', ')) }
Write-Output ("Fishing rod revision regression passed ({0} checks)." -f $checks.Count)
