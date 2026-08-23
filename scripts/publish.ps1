# Publish TecSight as a self-contained single-file win-x64 exe
param(
    [string]$Output = "publish",
    [string]$Runtime = "win-x64"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet publish (Join-Path $root "src/TecSight.App") -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $root $Output)
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}
$exe = Join-Path $root (Join-Path $Output "TecSight.App.exe")
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Publish output not found: $exe"
}
Write-Host "Output: $exe"
