# 发布 TecSight 为 win-x64 自包含单文件 exe
param(
    [string]$Output = "publish",
    [string]$Runtime = "win-x64"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet publish (Join-Path $root "src/TecSight.App") -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $root $Output)
Write-Host "Output: $(Join-Path $root $Output)\TecSight.App.exe"