# TecSight v1.2.0 — Release Notes

> 一款 Windows 硬件体检工具：查看设备**所有硬件清单**与**实时使用/运行情况**。跨硬件兼容、免安装、免管理员。
> A Windows hardware inspector: see your device's **full hardware inventory** and **live usage/status**. Cross-hardware compatible, no install, no admin.

## 功能亮点 / Highlights
- **硬件清单**：CPU（架构/缓存/插槽/频率/ID）、内存（SPD）、磁盘（介质/总线/固件/健康度/SMART）、显卡（显存/驱动）、主板与系统、网卡（速率/IP/网关/DNS）、电池（容量/循环/化学/电压）、显示器（EDID）、音频/USB/键盘/鼠标/打印机
- **实时指标**：CPU 占用与频率、内存、GPU 占用与显存、磁盘 I/O、网络吞吐、电量、运行时长、进程排行（1 秒刷新）
- **传感器**：温度/风扇/电压（LibreHardwareMonitor）；磁盘 SMART；需内核驱动的传感器可「以管理员权限重启」读取
- **历史曲线**：最近 1 小时趋势
- **外设识别**：热插拔设备自动分类（自动刷新 + 手动刷新）
- **导出**：JSON / TXT / HTML / 复制摘要 / 兼容性报告 / 历史 CSV
- **中英双语 + 深浅主题**（窗口/导航/菜单/滚动条/按钮全部随主题），偏好持久化（主题/语言/窗口/噪音开关/上次页面）

## 兼容性 / Compatibility
- Windows 10/11 x64，自包含单文件，免安装免管理员
- 数据源不可用时优雅降级为「不可用」，无编造数据
- 0 警告构建，99 项单元测试

## 下载 / Download
- [TecSight-Setup-1.2.0-Windows-x64.exe](installer/Output/TecSight-Setup-1.2.0-Windows-x64.exe)（中英双语安装向导）
- 或直接运行 `publish/TecSight.App.exe`（自包含单文件）

## 变更摘要 / Changelog Summary
本版含性能优化（CPU 占用约降 60%）、深色模式全面主题化、传感器噪音过滤开关、记住上次页面、多项数据准确性修复（网卡速率哨兵值/区域格式/导出 N/A 等）。详见 [CHANGELOG.md](CHANGELOG.md)。

## 致谢 / Credits
CrystalDiskInfo / CPU-Z / GPU-Z / HWiNFO / LibreHardwareMonitor（MPL-2.0，见 THIRD-PARTY-NOTICES.md）
