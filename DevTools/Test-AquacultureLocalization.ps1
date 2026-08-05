[CmdletBinding()]
param()

$root = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $root 'Source'
$languageRoot = Join-Path $root '1.6\Languages'
$languageFiles = @(Get-ChildItem -LiteralPath $languageRoot -Recurse -Filter 'AquacultureFishing.xml' -File)
if ($languageFiles.Count -eq 0) {
    throw "No Aquaculture keyed language files were found under $languageRoot."
}

$sourceKeys = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.cs' -File) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in [regex]::Matches($text, '"(AquacultureFishing\.[A-Za-z0-9_]+)"')) {
        $null = $sourceKeys.Add($match.Groups[1].Value)
    }
}

# These prefixes are completed with the water habitat enum at runtime.
$null = $sourceKeys.Remove('AquacultureFishing.ConservationMigrationSource')
$null = $sourceKeys.Remove('AquacultureFishing.ConservationMigrationNoSource')
$null = $sourceKeys.Remove('AquacultureFishing.PondCapacityConstraint')

$requiredKeys = @(
    'AquacultureFishing.PondSafetyTitle',
    'AquacultureFishing.PondSafetyHealthy',
    'AquacultureFishing.PondSafetyInspect',
    'AquacultureFishing.ConservationCatchSafe',
    'AquacultureFishing.ConservationCatchUnavailable',
    'AquacultureFishing.PondHabitatTitle',
    'AquacultureFishing.PondInspectFallback'
)
foreach ($requiredKey in $requiredKeys) { $null = $sourceKeys.Add($requiredKey) }

$languageKeySets = @{}
foreach ($file in $languageFiles) {
    try { [xml]$document = Get-Content -LiteralPath $file.FullName -Raw }
    catch { throw "Invalid localization XML: $($file.FullName): $($_.Exception.Message)" }
    $elements = @($document.LanguageData.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })
    $duplicates = @($elements | Group-Object -Property Name | Where-Object { $_.Count -gt 1 })
    if ($duplicates.Count -gt 0) {
        throw "Duplicate Aquaculture translation keys in $($file.FullName): $($duplicates.Name -join ', ')"
    }
    $keys = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($element in $elements) { $null = $keys.Add($element.Name) }
    $languageKeySets[$file.FullName] = $keys
    $missing = @($sourceKeys | Where-Object { -not $keys.Contains($_) })
    if ($missing.Count -gt 0) {
        throw "Missing Aquaculture translation keys in $($file.FullName): $($missing -join ', ')"
    }
}

Write-Output ("Aquaculture localization contract passed for {0} language file(s) and {1} referenced key(s)." -f
    $languageFiles.Count, $sourceKeys.Count)
