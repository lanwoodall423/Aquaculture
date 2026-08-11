param([Parameter(Mandatory = $true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
try {
    $names = @($zip.Entries | ForEach-Object FullName)
    $allowed = @('About/', 'LoadFolders.xml', '1.6/')
    $bad = @($names | Where-Object {
        $name = $_
        -not (($allowed | Where-Object { $name -eq $_ -or $name.StartsWith($_) }).Count -gt 0) -or
        $name -match '(^|/)(DevTools|Source|bin|obj|Build|Runtime)(/|$)|\.pdb$|\.exe$|BridgeAdapter|InGameTest'
    })
    if ($bad.Count -gt 0) { throw "Invalid package entries: $($bad -join ', ')" }
    foreach ($required in @('About/About.xml', '1.6/Assemblies/AquacultureFishing.dll')) {
        if ($names -notcontains $required) { throw "Required package entry missing: $required" }
    }
    "Player package contract passed: $($names.Count) entries"
}
finally { $zip.Dispose() }
