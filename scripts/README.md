# 脚本说明

本目录提供构建与发布脚本（Windows PowerShell）：

| 脚本 | 用途 |
| --- | --- |
| `test.ps1` | 构建解决方案并运行全部测试 |
| `publish.ps1` | 发布 win-x64 自包含单文件 exe 到 `publish/` |
| `build-installer.ps1` | 发布最新版 + 编译 Inno Setup 安装程序（需安装 Inno Setup 6），输出到 `installer/Output/` |

> 说明：PowerShell 执行策略若禁用脚本，可用 `powershell -ExecutionPolicy Bypass -File scripts\test.ps1` 运行。