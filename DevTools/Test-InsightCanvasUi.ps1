$ErrorActionPreference = 'Stop'
dotnet run --project (Join-Path $PSScriptRoot 'InsightCanvasUiTests.csproj') --configuration Release --nologo
