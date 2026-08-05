[CmdletBinding()]
param()

$project = Join-Path $PSScriptRoot 'TraitBreedingRules.Tests.csproj'
if (-not (Test-Path -LiteralPath $project)) {
    throw "Missing executable trait-breeding test project: $project"
}

& dotnet run --project $project --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "TraitBreedingRules executable tests failed with exit code $LASTEXITCODE."
}
