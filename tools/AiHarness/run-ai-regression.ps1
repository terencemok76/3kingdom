$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..\..")
dotnet run --project "tools\AiHarness\AiHarness.csproj"
