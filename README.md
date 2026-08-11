# TecSight 硬件体检

[English](README.en.md) | **简体中文**

> 一款 Windows 桌面工具：查看设备**所有硬件清单**与**实时使用/运行情况**。跨硬件兼容，免安装、免管理员权限，缺失的传感器优雅降级显示"不可用"。

## 功能

- **硬件清单**：CPU、内存、磁盘、显卡、主板/系统、网卡、电池的完整静态信息
- **运行指标**：CPU / 内存 / GPU / 磁盘 I/O / 网络吞吐 / 电量，1 秒实时刷新
- **传感器与健康度**：温度、风扇转速、电压（LibreHardwareMonitor）；磁盘 SMART 健康度
- **历史曲线**：最近 1 小时趋势（环形缓冲）
- **导出报告**：一键导出 JSON / TXT / HTML 快照、复制摘要、兼容性自检报告
- **外设识别**：热插拔设备自动分类显示（存储/键盘/鼠标/摄像头/音频/蓝牙/打印机/USB）
- **中英双语 + 深浅主题**：界面可随时切换并记忆

## 界面

- 左侧导航：概览 / CPU / 内存 / 磁盘 / 显卡 / 主板与系统 / 网络 / 电池 / 传感器 / 进程 / 其他设备 / 外设
- 概览页：12 张卡片（各硬件型号 + 实时使用率/频率/温度/风扇/运行时长）
- 详情页：完整清单（架构/缓存/SPD/SMART/健康度/电压/化学类型等）+ 实时数值 + 历史曲线
- 外设页：热插拔设备自动分类识别（5 秒刷新 + 手动刷新）
- 其他设备页：显示器(EDID)/音频/USB/键盘/鼠标/打印机
- 中英双语 + 深色/浅色主题（记忆上次选择）；一键复制摘要 / 导出 JSON·TXT / 兼容性报告
- 免管理员；CPU 温度/风扇等需内核驱动的传感器可用"以管理员权限重启"读取

## 构建

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)（Windows 10/11 x64）。

```bash
dotnet build TecSight.slnx
dotnet test TecSight.slnx
dotnet publish src/TecSight.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish
```

发布产物：`publish/TecSight.App.exe`（自包含单文件，免安装）。也可直接运行 `scripts/publish.ps1`。

**安装程序**（需 [Inno Setup 6](https://jrsoftware.org/isdl.php)）：`./scripts/build-installer.ps1` → 生成 `installer/Output/TecSight-Setup-1.1.0.exe`（中英双语向导、开始菜单/桌面快捷方式、卸载器，免管理员权限）。

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