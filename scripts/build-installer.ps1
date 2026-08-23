# Package TecSight installer (requires Inno Setup 6)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

# 1) Publish the latest self-contained single file
dotnet publish (Join-Path $root "src/TecSight.App") -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $root "publish")
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}
$exe = Join-Path $root "publish\TecSight.App.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Publish output not found: $exe"
}

# 2) Locate the Inno Setup compiler
$iscc = Get-ChildItem `
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $iscc) { throw "ISCC.exe (Inno Setup 6) not found. Please install Inno Setup 6 first." }

# 3) Compile the installer
& $iscc.FullName (Join-Path $root "installer\tecsight.iss")
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE"
}
$setup = Get-ChildItem -LiteralPath (Join-Path $root "installer\Output") -Filter "TecSight*Setup*.exe" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $setup) {
    throw "Installer output file not found."
}
Write-Host "Setup: $($setup.FullName)"
