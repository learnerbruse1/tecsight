# 构建并运行全部测试
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
dotnet build TecSight.slnx
dotnet test TecSight.slnx