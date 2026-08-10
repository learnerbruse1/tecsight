# 打包 TecSight 安装程序（需 Inno Setup 6）
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

# 1) 发布最新自包含单文件
dotnet publish (Join-Path $root "src/TecSight.App") -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $root "publish")

# 2) 定位 Inno Setup 编译器
$iscc = Get-ChildItem `
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $iscc) { throw "未找到 ISCC.exe（Inno Setup 6），请先安装 Inno Setup。" }

# 3) 编译安装程序
& $iscc.FullName (Join-Path $root "installer\tecsight.iss")
Write-Host "Setup: $(Join-Path $root 'installer\Output\TecSight-Setup-1.0.0.exe')"