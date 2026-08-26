# TecSight Hardware Inspector

**English** | [简体中文](README.md)

> A Windows desktop tool that shows your device's **complete hardware inventory** and **live usage/status**. Cross-hardware compatible, no install, no admin rights; sensors that cannot be read degrade gracefully to "N/A".

## Features

- **Hardware inventory**: CPU (architecture/cache/socket/frequency/ID), memory (full SPD), storage (media/bus/firmware/health/SMART), GPU (VRAM/driver), motherboard & OS (BIOS/firmware/install info), network (speed/IP/gateway/DNS), battery (capacity/cycles/chemistry/voltage), displays (EDID), audio / USB / keyboards / mice / printers
- **Live metrics**: CPU usage & live clock, memory, GPU usage & VRAM, disk I/O, network throughput, battery, uptime, process ranking — **1-second refresh** (overview shows last-updated time)
- **Sensors & health**: temperatures / fan speeds / voltages (LibreHardwareMonitor + Windows ACPI thermal-zone fallback); disk SMART attributes & health; CPU temp/fans need admin rights on some systems, and hardware that doesn't expose them shows "N/A"
- **History charts**: last-hour trends (ring buffer, tiny memory footprint)
- **Peripherals**: hotplug devices auto-classified (storage/keyboard/mouse/camera/audio/Bluetooth/printer/USB/physical network adapters), auto refresh + manual refresh, deduplicated by PNP ID
- **Export**: JSON / TXT / HTML / copy summary / compatibility report / history CSV
- **Bilingual UI + themes**: switchable and remembered; window position/size/theme/language persisted

## UI

- Left navigation: Overview / CPU / Memory / Storage / GPU / Motherboard & OS / Network / Battery / Sensors / Processes / Peripherals
- Overview page: 12 cards (CPU / memory / disk / GPU / VRAM usage / network / temperatures / fan / uptime / battery / system)
- Detail pages: full inventory + live values + history charts (1 hour)
- Sensors page: all sensor readings, with an optional 'hide network filter noise' toggle (preference is remembered)
- Processes page: top processes by CPU/memory (with PID) and total process count
- Peripherals page: hotplug device classification (auto refresh + manual refresh), including displays (EDID) / audio / USB / keyboards / mice / printers / physical network adapters
- Top bar: theme toggle 🌙 / language toggle / export menu (JSON·TXT·HTML·history CSV·copy summary·compatibility report) / restart as admin
- A prominent banner is shown on the overview when a data source (performance counters/WMI) is unavailable
- Shortcuts: Ctrl+E opens the export menu, F5 refreshes, F11 toggles dark/light theme

## Compatibility

- Windows 10/11 (x64), self-contained single file, **no install, no admin** (admin mode optional for CPU temp/fans)
- Degrades gracefully to "N/A" when sensors/data sources are unavailable; all values come from real system APIs (performance counters/WMI/native APIs/LibreHardwareMonitor/HidSharp/ManagedNativeWifi/Nefarius.Utilities.DeviceManagement) — no fabricated data

## Build

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) (Windows 10/11 x64).

```bash
dotnet build TecSight.slnx
dotnet test TecSight.slnx
dotnet publish src/TecSight.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Output: `publish/TecSight.App.exe` (self-contained single file, no install needed). You can also run `scripts/publish.ps1`; build+test with `scripts/test.ps1`.

**Installer** (requires [Inno Setup 6](https://jrsoftware.org/isdl.php)): `./scripts/build-installer.ps1` → produces `installer/Output/TecSight-Setup-2.1.0-Windows-x64.exe` (bilingual wizard, Start Menu/desktop shortcuts, uninstaller, no admin needed).

## Tech Stack

- C# / .NET 10 / WPF
- Data sources: performance counters + native APIs, WMI, [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL-2.0), [HidSharp](https://software.seekye.com/hidsharp) (Apache-2.0), [Vanara](https://github.com/dahall/Vanara) (MIT), [ManagedNativeWifi](https://github.com/emoacht/ManagedNativeWifi) (MIT), [Nefarius.Utilities.DeviceManagement](https://github.com/nefarius/Nefarius.Utilities.DeviceManagement) (MIT)

## References & Credits

- [CrystalDiskInfo](https://crystalmark.info/en/software/crystaldiskinfo/) — SMART health approach
- [CPU-Z](https://www.cpuid.com/softwares/cpu-z.html) / [GPU-Z](https://www.techpowerup.com/gpuz/) — hardware inventory presentation
- [HWiNFO](https://www.hwinfo.com/) / [OpenHardwareMonitor](https://openhardwaremonitor.org/) — sensor reading approach (via LibreHardwareMonitor)
- [GPUView](https://github.com/microsoft/etwtraces) — GPU utilization statistics approach

## License

- This project code: MIT (see [LICENSE](LICENSE))
- Integrates [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL-2.0), [HidSharp](https://software.seekye.com/hidsharp) (Apache-2.0), [Vanara](https://github.com/dahall/Vanara) (MIT), [ManagedNativeWifi](https://github.com/emoacht/ManagedNativeWifi) (MIT), [Nefarius.Utilities.DeviceManagement](https://github.com/nefarius/Nefarius.Utilities.DeviceManagement) (MIT); see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)
