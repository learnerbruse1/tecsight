# TecSight Hardware Inspector

**English** | [简体中文](README.md)

> A Windows desktop tool that shows your device's **complete hardware inventory** and **live usage/status**. Cross-hardware compatible, no install, no admin rights — sensors that cannot be read degrade gracefully to "N/A".

## Features

- **Hardware inventory**: full static info for CPU, memory, storage, GPU, motherboard/OS, network adapters, battery
- **Live metrics**: CPU / memory / GPU / disk I/O / network throughput / battery, refreshed every second
- **Sensors & health**: temperatures, fan speeds, voltages (LibreHardwareMonitor); disk SMART health
- **History charts**: last-hour trends (ring buffer)
- **Export**: one-click JSON / TXT snapshot export
- **Bilingual UI**: switch between Chinese and English anytime (follows system language by default)

## UI

- Left navigation: Overview / CPU / Memory / Storage / GPU / Motherboard & OS / Network / Battery / Sensors
- Overview page: key usage cards + key temperature
- Detail pages: full inventory + live values + history charts

## Build

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) (Windows 10/11 x64).

```bash
dotnet build TecSight.slnx
dotnet test TecSight.slnx
dotnet publish src/TecSight.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish
```

Output: `publish/TecSight.App.exe` (self-contained single file, no install needed).

## Tech Stack

- C# / .NET 10 / WPF
- Data sources: performance counters + native APIs, WMI, [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL-2.0)

## References & Credits

- [CrystalDiskInfo](https://crystalmark.info/en/software/crystaldiskinfo/) — SMART health approach
- [CPU-Z](https://www.cpuid.com/softwares/cpu-z.html) / [GPU-Z](https://www.techpowerup.com/gpuz/) — hardware inventory presentation
- [HWiNFO](https://www.hwinfo.com/) / [OpenHardwareMonitor](https://openhardwaremonitor.org/) — sensor reading approach (via LibreHardwareMonitor)
- [GPUView](https://github.com/microsoft/etwtraces) — GPU utilization statistics approach

## License

- This project code: MIT (see [LICENSE](LICENSE))
- Integrates [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL-2.0); its file header notices are retained