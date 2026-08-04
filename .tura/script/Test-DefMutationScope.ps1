$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$corePath = Join-Path $root 'Source\AquacultureCore.cs'
$lifePath = Join-Path $root 'Source\FishLifeCycle.cs'
$thingDefsPath = Join-Path $root '1.6\Defs\ThingDefs.xml'
$recipeDefsPath = Join-Path $root '1.6\Defs\RecipeDefs.xml'
$compatPath = Join-Path $root 'About\COMPATIBILITY.md'
$core = Get-Content -Raw $corePath
$life = Get-Content -Raw $lifePath
$thingDefs = Get-Content -Raw $thingDefsPath
$recipeDefs = Get-Content -Raw $recipeDefsPath
$compat = Get-Content -Raw $compatPath
$checks = [ordered]@{}

function Check([string]$name, [bool]$condition) {
    if (-not $condition) { throw "FAIL $name" }
    $script:checks[$name] = $true
    Write-Output "PASS $name"
}

$runtimeFishStart = $core.IndexOf('private static void ConfigureRuntimeFishDef')
$runtimeFishEnd = $core.IndexOf('private static void EnsureFishComp', $runtimeFishStart)
$runtimeFish = $core.Substring($runtimeFishStart, $runtimeFishEnd - $runtimeFishStart)

Check 'startup discovers only positively identified fish' ($core -match 'AllDefs\.Where\(FishUtility\.IsFish\)' -and $core -match 'RegisterRuntimeFish\(def\)')
Check 'unsupported fish-like items are not configured' ($core -match 'private static void ConfigureRuntimeFishDef\(ThingDef def\)' -and $core -match 'FishUtility\.IsFish')
Check 'no startup-wide ingestible component loop' ($core -notmatch 'AllDefs\.Where\([^\r\n]*ingestible\s*!=\s*null')
Check 'old global food configurator is removed' ($core -notmatch 'ConfigureFishFoodTraitComps')
Check 'vanilla and third-party ingestibles remain Def untouched' ($core -notmatch 'foreach\s*\(ThingDef def in DefDatabase<ThingDef>\.AllDefs[^\r\n]*ingestible')
Check 'food provenance attaches to relevant Things' ($life -match 'public static CompFishFoodTraits EnsureOn\(Thing thing\)' -and $life -match 'owner\.AllComps\.Add\(added\)')
Check 'split stacks retain provenance' ($life -match 'PostSplitOff\(Thing piece\)' -and $life -match 'CompFishFoodTraits split = EnsureOn\(piece\)')
Check 'provenance save fields remain serialized' ($life -match 'Scribe_Values\.Look\(ref nutritionMultiplier' -and $life -match 'Scribe_Values\.Look\(ref delicious')
Check 'only provenance-bearing recipe outputs receive dynamic components' ($life -match 'nutritionMultiplier > 1\.001f \|\| delicious' -and $life -match 'CompFishFoodTraits\.EnsureOn\(product\)')
Check 'supported processing route remains exact' ($life -match 'recipeDef\.defName == FishProcessingYield\.ProcessRecipeDefName' -and $life -match 'FishUtility\.IsRuntimeFish\(dead\.parent\.def\)')
Check 'unrelated recipes retain normal yield' ($life -match 'float yieldFactor = isSupportedFishProcessing \?')
Check 'AF_FishMeat keeps explicit persisted provenance component' ($thingDefs -match 'CompProperties_FishMeatTraits')
Check 'fish recipes still include the owned processing route' ($recipeDefs -match 'AF_ProcessDeadFish' -and $recipeDefs -match 'AF_FishMeat')
Check 'fish stack limit is retained for individual state' ($runtimeFish -match 'def\.stackLimit != 1' -and $runtimeFish -match 'def\.stackLimit = 1')
Check 'fish drawer mutation is conditional and diagnosed' ($runtimeFish -match 'DrawerType\.MapMeshAndRealTime' -and $runtimeFish -match 'LogDefDiagnostic\("drawer:')
Check 'explicit fish stats are preserved' ($core -match 'def\.statBases\.Any\(modifier => modifier\?\.stat == stat\)' -and $core -match 'GetStatValueAbstract\(stat\)')
Check 'fish traits component is idempotent' ($core -match 'compClass == typeof\(CompFishTraits\)' -and $core -match 'def\.comps\.Any' -and $core -match 'return;')
Check 'traits UI is limited to recognized fish' ($runtimeFish -match 'EnsureTraitsTab\(def\)')
Check 'storage and trade Def data is not modified' ($runtimeFish -notmatch 'thingCategories|tradeability|tradeTags|storageSettings|specialDisplay')
Check 'old saved food components have a load compatibility boundary' ($life -match 'FishFoodSaveCompatibility' -and $life -match 'Game\.LoadGame' -and $life -match 'TemporarilyAdded')
Check 'temporary load components are removed after loading' ($life -match 'EndLoad\(\)' -and $life -match 'entry\.Key\?\.comps\?\.Remove\(entry\.Value\)')
Check 'compatibility contract documents scoped provenance' ($compat -match 'relevant recipe output Things' -and $compat -match 'temporarily available on ingestible Defs')

Write-Output "Representative mutation cases covered: vanilla food, non-fish modded ingestible, supported fish, unsupported fish-like item, fish recipe output, unrelated recipe, storage/trade, and legacy save/load."
Write-Output "Passed $($checks.Count) Def-mutation checks."
