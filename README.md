# TecSight 硬件体检

[English](README.en.md) | **简体中文**

> 一款 Windows 桌面工具：查看设备**所有硬件清单**与**实时使用/运行情况**。跨硬件兼容，免安装、免管理员权限，缺失的传感器优雅降级显示"不可用"。

## 功能

- **硬件清单**：CPU、内存、磁盘、显卡、主板/系统、网卡、电池的完整静态信息
- **运行指标**：CPU / 内存 / GPU / 磁盘 I/O / 网络吞吐 / 电量，1 秒实时刷新
- **传感器与健康度**：温度、风扇转速、电压（LibreHardwareMonitor）；磁盘 SMART 健康度
- **历史曲线**：最近 1 小时趋势（环形缓冲）
- **导出报告**：一键导出 JSON / TXT 快照
- **中英双语**：界面可随时切换（默认跟随系统语言）

## 界面

- 左侧导航：概览 / CPU / 内存 / 磁盘 / 显卡 / 主板与系统 / 网络 / 电池 / 传感器
- 概览页：关键使用率卡片 + 关键温度
- 详情页：完整清单 + 实时数值 + 历史曲线

## 构建

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)（Windows 10/11 x64）。

```bash
dotnet build TecSight.slnx
dotnet test TecSight.slnx
dotnet publish src/TecSight.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish
```

发布产物：`publish/TecSight.App.exe`（自包含单文件，免安装）。也可直接运行 `scripts/publish.ps1`。`n`n**安装程序**（需 [Inno Setup 6](https://jrsoftware.org/isdl.php)）：`./scripts/build-installer.ps1` → 生成 `installer/Output/TecSight-Setup-1.0.0.exe`（中英双语向导、开始菜单/桌面快捷方式、卸载器，免管理员权限）。

## 技术栈

- C# / .NET 10 / WPF
- 数据源：性能计数器 + 原生 API、WMI、[LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)（MPL-2.0）

## 借鉴与致谢

- [CrystalDiskInfo](https://crystalmark.info/en/software/crystaldiskinfo/) — SMART 健康度思路
- [CPU-Z](https://www.cpuid.com/softwares/cpu-z.html) / [GPU-Z](https://www.techpowerup.com/gpuz/) — 硬件清单展示思路
- [HWiNFO](https://www.hwinfo.com/) / [OpenHardwareMonitor](https://openhardwaremonitor.org/) — 传感器读取思路（经由 LibreHardwareMonitor 实现）
- [GPUView](https://github.com/microsoft/etwtraces) — GPU 占用统计思路

## 许可证

- 本项目代码：MIT（见 [LICENSE](LICENSE)）
- 集成 [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)（MPL-2.0），其文件头声明予以保留