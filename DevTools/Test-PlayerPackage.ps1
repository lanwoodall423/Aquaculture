param([Parameter(Mandatory = $true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
try {
    $names = @($zip.Entries | ForEach-Object FullName)
    # Only 1.7 player runtime content plus the three shared mod metadata files.
    $allowed = @('About/', 'LICENSE', 'LoadFolders.xml', '1.7/')
    $bad = @($names | Where-Object {
        $name = $_
        -not (($allowed | Where-Object { $name -eq $_ -or $name.StartsWith($_) }).Count -gt 0) -or
        $name -match '(^|/)1[.]6(/|$)|(^|/)(DevTools|Source|bin|obj|Build|Runtime)(/|$)|\.pdb$|\.exe$|BridgeAdapter|InGameTest'
    })
    if ($bad.Count -gt 0) { throw "Invalid package entries: $($bad -join ', ')" }

    # A permissive 1.7/ prefix by itself is not enough: reject unknown folders,
    # binaries and extra metadata that could create a second mod runtime.
    $unexpected = @($names | Where-Object {
        $name = $_
        if ($name -eq 'LICENSE' -or $name -eq 'LoadFolders.xml' -or
            $name -eq 'About/' -or $name -eq 'About/About.xml' -or $name -eq '1.7/') { return $false }
        if ($name.EndsWith('/')) {
            return -not ($name -match '^1[.]7/(Defs|Languages|Textures|Assemblies|DeferredReality)(/|$)')
        }
        return -not (
            $name -match '^1[.]7/Defs/.+[.]xml$' -or
            $name -match '^1[.]7/Languages/.+[.]xml$' -or
            $name -match '^1[.]7/Textures/.+[.]png$' -or
            $name -eq '1.7/Assemblies/AquacultureFishing.dll' -or
            $name -eq '1.7/DeferredReality/Assemblies/DeferredReality.Aquaculture.dll' -or
            $name -eq '1.7/DeferredReality/Assemblies/DeferredReality.Aquaculture.manifest.json'
        )
    })
    if ($unexpected.Count -gt 0) {
        throw "Unexpected content inside the 1.7 player runtime: $($unexpected -join ', ')"
    }

    foreach ($required in @(
        'About/About.xml', 'LoadFolders.xml',
        '1.7/Defs/ThingDefs.xml',
        '1.7/Languages/English/Keyed/AquacultureFishing.xml',
        '1.7/Textures/Things/Item/Equipment/FishingRod.png',
        '1.7/Assemblies/AquacultureFishing.dll'
    )) {
        if ($names -notcontains $required) { throw "Required package entry missing: $required" }
    }

    $baseAssemblies = @($names | Where-Object { $_ -match '^1[.]7/Assemblies/[^/]+$' })
    if ($baseAssemblies.Count -ne 1 -or $baseAssemblies[0] -ne '1.7/Assemblies/AquacultureFishing.dll') {
        throw 'Unconditional Assemblies folder must contain only AquacultureFishing.dll.'
    }

    $aboutEntry = $zip.GetEntry('About/About.xml')
    $aboutReader = [IO.StreamReader]::new($aboutEntry.Open())
    try { [xml]$aboutContract = $aboutReader.ReadToEnd() }
    finally { $aboutReader.Dispose() }
    $versions = @($aboutContract.SelectNodes('/ModMetaData/supportedVersions/li'))
    if ($versions.Count -ne 1 -or $versions[0].InnerText.Trim() -ne '1.7') {
        throw 'Packaged About.xml must advertise RimWorld 1.7 exclusively.'
    }

    $loadFolderEntry = $zip.GetEntry('LoadFolders.xml')
    $loadFolderReader = [IO.StreamReader]::new($loadFolderEntry.Open())
    try { [xml]$folderContract = $loadFolderReader.ReadToEnd() }
    finally { $loadFolderReader.Dispose() }
    $versionsInLoader = @($folderContract.SelectNodes('/loadFolders/*'))
    $coreFolder = $folderContract.SelectSingleNode("/loadFolders/v1.7/li[not(@*) and text()='1.7']")
    $conditional = $folderContract.SelectSingleNode("/loadFolders/v1.7/li[@IfModActive='lan.deferredreality.framework' and text()='1.7/DeferredReality']")
    if ($versionsInLoader.Count -ne 1 -or $versionsInLoader[0].Name -ne 'v1.7' -or
        @($folderContract.SelectNodes('/loadFolders/v1.7/li')).Count -ne 2 -or
        $null -eq $coreFolder -or $null -eq $conditional -or $conditional.Attributes.Count -ne 1) {
        throw 'DRF provider is not conditional in the RimWorld 1.7 LoadFolders.xml.'
    }

    $providerDllPath = '1.7/DeferredReality/Assemblies/DeferredReality.Aquaculture.dll'
    $providerManifestPath = '1.7/DeferredReality/Assemblies/DeferredReality.Aquaculture.manifest.json'
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
            $providerManifest.sha256 -ne $actualHash -or
            $providerManifest.semanticApiVersion -lt 2 -or
            $providerManifest.providerSchemaVersion -lt 2) {
            throw 'Packaged optional DRF provider hash, schema or identity mismatch.'
        }
    }
    "Player package contract passed: $($names.Count) entries; optional DRF provider present: $hasDll"
}
finally { $zip.Dispose() }
