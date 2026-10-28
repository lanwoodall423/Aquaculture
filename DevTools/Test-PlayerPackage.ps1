param([Parameter(Mandatory = $true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
try {
    $names = @($zip.Entries | ForEach-Object FullName)
    $allowed = @('About/', 'LICENSE', 'LoadFolders.xml', '1.6/')
    $bad = @($names | Where-Object {
        $name = $_
        -not (($allowed | Where-Object { $name -eq $_ -or $name.StartsWith($_) }).Count -gt 0) -or
        $name -match '(^|/)(DevTools|Source|bin|obj|Build|Runtime)(/|$)|\.pdb$|\.exe$|BridgeAdapter|InGameTest'
    })
    if ($bad.Count -gt 0) { throw "Invalid package entries: $($bad -join ', ')" }
    foreach ($required in @('About/About.xml', 'LoadFolders.xml', '1.6/Assemblies/AquacultureFishing.dll')) {
        if ($names -notcontains $required) { throw "Required package entry missing: $required" }
    }
    $baseAssemblies = @($names | Where-Object { $_ -match '^1[.]6/Assemblies/[^/]+[.]dll$' })
    if ($baseAssemblies.Count -ne 1 -or $baseAssemblies[0] -ne '1.6/Assemblies/AquacultureFishing.dll') {
        throw 'Unconditional Assemblies folder must contain only AquacultureFishing.dll.'
    }
    $providerDllPath = '1.6/DeferredReality/Assemblies/DeferredReality.Aquaculture.dll'
    $providerManifestPath = '1.6/DeferredReality/Assemblies/DeferredReality.Aquaculture.manifest.json'
    $allowedProviderPaths = @($providerDllPath, $providerManifestPath)
    $unexpectedProviderPaths = @($names | Where-Object {
        $_ -match 'DeferredReality[.]Aquaculture[.](dll|manifest[.]json)$' -and
        $allowedProviderPaths -notcontains $_
    })
    if ($unexpectedProviderPaths.Count -ne 0) {
        throw "Deferred Reality provider outside conditional folder: $($unexpectedProviderPaths -join ', ')"
    }
    $hasDll = $names -contains $providerDllPath
    $hasManifest = $names -contains $providerManifestPath
    if ($hasDll -ne $hasManifest) { throw 'Optional DRF provider DLL/manifest pair is incomplete.' }

    $loadFolderEntry = $zip.GetEntry('LoadFolders.xml')
    $loadFolderReader = [IO.StreamReader]::new($loadFolderEntry.Open())
    try { [xml]$folderContract = $loadFolderReader.ReadToEnd() }
    finally { $loadFolderReader.Dispose() }
    $conditional = $folderContract.SelectSingleNode("/loadFolders/v1.6/li[@IfModActive='lan.deferredreality.framework' and text()='1.6/DeferredReality']")
    if ($null -eq $conditional) { throw 'DRF provider is not conditional in LoadFolders.xml.' }

    if ($hasDll) {
        $providerEntry = $zip.GetEntry($providerDllPath)
        $manifestReader = [IO.StreamReader]::new($zip.GetEntry($providerManifestPath).Open())
        try { $providerManifest = $manifestReader.ReadToEnd() | ConvertFrom-Json }
        finally { $manifestReader.Dispose() }
        $providerStream = $providerEntry.Open()
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try { $actualHash = [BitConverter]::ToString($sha256.ComputeHash($providerStream)).Replace('-', '') }
        finally {
            $providerStream.Dispose()
            $sha256.Dispose()
        }
        if ($providerManifest.providerId -ne 'lan.aquaculture.natural-water' -or
            $providerManifest.assemblyName -ne 'DeferredReality.Aquaculture' -or
            $providerManifest.sha256 -ne $actualHash) {
            throw 'Packaged optional DRF provider hash or identity mismatch.'
        }
    }
    "Player package contract passed: $($names.Count) entries; optional DRF provider present: $hasDll"
}
finally { $zip.Dispose() }
