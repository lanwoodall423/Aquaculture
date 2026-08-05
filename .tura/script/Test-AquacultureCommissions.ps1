[CmdletBinding()]
param()

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$project = Join-Path $root 'DevTools\TraitBreedingRules.Tests.csproj'
if (-not (Test-Path -LiteralPath $project)) {
    throw "Missing executable commission test project: $project"
}

& dotnet run --project $project --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Executable commission and pure-logic tests failed with exit code $LASTEXITCODE."
}
