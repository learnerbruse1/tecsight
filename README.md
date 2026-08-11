# TecSight 硬件体检

[English](README.en.md) | **简体中文**

> 一款 Windows 桌面工具：查看设备**所有硬件清单**与**实时使用/运行情况**。跨硬件兼容，免安装、免管理员权限；缺失的传感器优雅降级显示"不可用"。

## 功能

- **硬件清单**：CPU（架构/缓存/插槽/频率/ID）、内存（SPD 全字段）、磁盘（介质/总线/固件/健康度/SMART）、显卡（显存/驱动）、主板与系统（BIOS/固件/安装信息）、网卡（速率/IP/网关/DNS）、电池（容量/循环/化学/电压）、显示器（EDID）、音频 / USB / 键盘 / 鼠标 / 打印机
- **运行指标**：CPU 占用与实时频率、内存、GPU 占用与显存、磁盘 I/O、网络吞吐、电量、运行时长、进程排行，**1 秒实时刷新**（概览显示最近更新时刻）
- **传感器与健康度**：温度 / 风扇 / 电压（LibreHardwareMonitor）；磁盘 SMART 属性与健康度；CPU 温度/风扇等需内核驱动的传感器可经「以管理员权限重启」读取
- **历史曲线**：最近 1 小时趋势（环形缓冲，内存占用极小）
- **外设识别**：热插拔设备自动分类显示（存储/键盘/鼠标/摄像头/音频/蓝牙/打印机/USB），5 秒刷新 + 手动刷新
- **导出**：JSON / TXT / HTML / 复制摘要 / 兼容性自检报告 / 历史 CSV
- **中英双语 + 深浅主题**：切换并记忆；窗口位置/大小/主题/语言持久化

## 界面

- 左侧导航：概览 / CPU / 内存 / 磁盘 / 显卡 / 主板与系统 / 网络 / 电池 / 传感器 / 进程 / 其他设备 / 外设
- 概览页：12 张卡片（各硬件型号 + 实时使用率/频率/温度/风扇/运行时长/更新时刻）
- 详情页：完整清单 + 实时数值 + 历史曲线（1 小时）
- 进程页：Top 进程 CPU/内存（含 PID）与进程总数
- 外设页：热插拔设备分类识别（5 秒刷新 + 手动刷新）
- 其他设备页：显示器(EDID)/音频/USB/键盘/鼠标/打印机
- 顶栏：主题切换 🌙 / 语言切换 / 导出菜单（JSON·TXT·HTML·历史CSV·复制摘要·兼容性报告）/ 以管理员权限重启
- 数据源异常时（性能计数器/WMI 不可用）概览页显示醒目提示横幅

## 兼容性

- 支持 Windows 10/11（x64），自包含单文件，**免安装、免管理员**（读 CPU 温度/风扇等可选管理员模式）
- 传感器/数据源不可用时优雅降级为"不可用"，不报错不崩溃；所有数值来自真实系统 API（性能计数器/WMI/原生 API/LibreHardwareMonitor），无编造数据

## 构建

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)（Windows 10/11 x64）。

```bash
dotnet build TecSight.slnx
dotnet test TecSight.slnx
dotnet publish src/TecSight.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

发布产物：`publish/TecSight.App.exe`（自包含单文件，免安装）。也可直接运行 `scripts/publish.ps1`；一键构建+测试用 `scripts/test.ps1`。

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
- 集成 [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)（MPL-2.0），其文件头声明予以保留（见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)）