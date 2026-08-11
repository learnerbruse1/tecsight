using System.Text;
using System.Text.Json;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>快照导出服务：把快照序列化为 JSON 或人类可读的 TXT。</summary>
public interface ISnapshotExporter
{
    string ExportJson(Snapshot snapshot);
    string ExportTxt(Snapshot snapshot);
}

/// <summary>默认快照导出器。</summary>
public sealed class SnapshotExporter : ISnapshotExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string ExportJson(Snapshot snapshot) => JsonSerializer.Serialize(snapshot, JsonOptions);

    public string ExportTxt(Snapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TecSight 快照报告 / TecSight Snapshot Report");
        sb.AppendLine($"采集时间 Captured at: {snapshot.CapturedAt:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();

        var inv = snapshot.Inventory;
        sb.AppendLine("[硬件清单 Hardware Inventory]");
        sb.AppendLine($"  计算机 Computer: {inv.ComputerName ?? "不可用 N/A"}");
        sb.AppendLine($"  系统 OS: {inv.OsCaption ?? "不可用 N/A"} {inv.OsVersion ?? ""}  {inv.OsArchitecture ?? ""}  {inv.FirmwareType ?? ""}".TrimEnd());
        sb.AppendLine($"  安装 Installed: {inv.OsInstallDate ?? "不可用 N/A"}  上次启动 Last Boot: {inv.LastBootTime ?? "不可用 N/A"}");
        foreach (var c in inv.Cpus)
        {
            sb.AppendLine($"  CPU: {c.Name ?? "不可用 N/A"}  {c.CoreCount}核/{c.LogicalProcessorCount}线程  {c.Architecture ?? ""}  L2={c.L2CacheKb ?? 0}KB L3={c.L3CacheKb ?? 0}KB".TrimEnd());
        }
        sb.AppendLine($"  内存 Memory: {inv.MemoryModules.Count} 条 / {FormatBytes(inv.MemoryModules.Sum(x => long.TryParse(x.CapacityBytes, out var b) ? b : 0))}");
        foreach (var d in inv.Disks)
        {
            sb.AppendLine($"  磁盘 Disk: {d.Model ?? "不可用 N/A"}  {FormatBytes(d.CapacityBytes)}  {d.MediaType ?? ""}  {d.BusType ?? ""}  FW {d.FirmwareVersion ?? "N/A"}  健康度 Health: {HealthText(d.Health)}".TrimEnd());
        }
        sb.AppendLine($"  GPU: {string.Join("; ", inv.Gpus.Select(g => g.Name ?? "不可用 N/A"))}");
        if (inv.Motherboard is { } mb)
        {
            sb.AppendLine($"  主板 Motherboard: {mb.Manufacturer ?? "N/A"} {mb.Product ?? ""}  BIOS {mb.BiosVersion ?? "N/A"}  {mb.SystemManufacturer ?? ""} {mb.SystemModel ?? ""}".TrimEnd());
        }
        foreach (var n in inv.NetworkAdapters)
        {
            sb.AppendLine($"  网卡 Network: {n.Name ?? "不可用 N/A"}  {n.MacAddress ?? "N/A"}  {LinkSpeed(n.SpeedBps)}  {n.AdapterType ?? ""}".TrimEnd());
        }
        if (inv.Battery is { } bat)
        {
            var health = bat.FullChargeCapacityWh is double full && bat.DesignedCapacityWh is double design && design > 0
                ? $"{Math.Min(100, full / design * 100):0.0}%"
                : "不可用 N/A";
            sb.AppendLine($"  电池 Battery: {bat.DeviceName ?? "不可用 N/A"}  设计 {bat.DesignedCapacityWh?.ToString("0.0") ?? "N/A"}Wh  满充 {bat.FullChargeCapacityWh?.ToString("0.0") ?? "N/A"}Wh  循环 {bat.CycleCount?.ToString() ?? "N/A"}  化学 {bat.Chemistry ?? "N/A"}  设计电压 {bat.DesignVoltageV?.ToString("0.00") ?? "N/A"}V  当前电压 {bat.CurrentVoltageV?.ToString("0.00") ?? "N/A"}V  健康度 {health}");
        }
        sb.AppendLine();
        sb.AppendLine("[其他设备 Other Devices]");
        sb.AppendLine($"  显示器 Displays: {inv.Displays.Count}  {string.Join("; ", inv.Displays.Select(d => $"{d.Manufacturer ?? ""} {d.Name ?? ""}".Trim()).Where(s => s.Length > 0))}");
        sb.AppendLine($"  音频 Audio: {inv.AudioDevices.Count}   USB: {inv.UsbDevices.Count}   键盘 Keyboards: {inv.Keyboards.Count}   鼠标 Mice: {inv.PointingDevices.Count}   打印机 Printers: {inv.Printers.Count}");
        sb.AppendLine();

        var m = snapshot.Metrics;
        sb.AppendLine("[运行指标 Live Metrics]");
        sb.AppendLine($"  CPU 占用 Usage: {FormatPct(m.CpuUsagePercent)}  频率 Freq: {FormatMhz(m.CpuFrequencyMhz)}  运行时长 Uptime: {FormatUptime(m.SystemUptimeSeconds)}");
        sb.AppendLine($"  内存 Memory: {FormatPct(m.MemoryUsagePercent)} ({FormatBytes(m.MemoryUsedBytes)} / {FormatBytes(m.MemoryTotalBytes)})");
        sb.AppendLine($"  GPU 占用 Usage: {FormatPct(m.GpuUsagePercent)}  引擎 Engines: {string.Join(", ", m.GpuEngines.Select(e => $"{e.EngineType} {e.Percent:0.0}%"))}");
        sb.AppendLine($"  磁盘 Disk: ↓读 {FormatBps(m.DiskReadBytesPerSec)} ↑写 {FormatBps(m.DiskWriteBytesPerSec)}");
        sb.AppendLine($"  网络 Network: ↓{FormatBps(m.NetworkDownloadBps)} ↑{FormatBps(m.NetworkUploadBps)}");
        sb.AppendLine($"  电池 Battery: {FormatPct(m.BatteryChargePercent)} {(m.BatteryIsCharging == true ? "充电 Charging" : "")}");
        sb.AppendLine($"  进程 Processes: {m.Processes.Count}  SMART 属性: {m.SmartAttributes.Count}  传感器 Sensors: {m.Sensors.Count}");
        sb.AppendLine();

        sb.AppendLine("[传感器读数 Sensor Readings]");
        foreach (var s in m.Sensors.Take(60))
        {
            sb.AppendLine($"  {s.HardwareName} / {s.SensorName}: {s.Value?.ToString("0.#") ?? "不可用 N/A"} {s.Unit}");
        }
        if (m.Sensors.Count > 60)
        {
            sb.AppendLine($"  … 其余 {m.Sensors.Count - 60} 条（详见界面传感器页）");
        }
        if (m.Sensors.Count == 0)
        {
            sb.AppendLine("  （无传感器数据 No sensor data）");
        }

        return sb.ToString();
    }

    private static string HealthText(StorageHealth? h) => h?.Status switch
    {
        HealthStatus.Good => "良好 Good",
        HealthStatus.Warning => "注意 Warning",
        HealthStatus.Critical => "危险 Critical",
        _ => "不可用 N/A",
    };

    private static string FormatPct(double? v) => v.HasValue ? $"{v.Value:0.0}%" : "不可用 N/A";
    private static string FormatMhz(double? v) => v.HasValue ? $"{v.Value:0} MHz" : "不可用 N/A";
    private static string FormatBytes(double? b) => b.HasValue ? $"{b.Value / (1024.0 * 1024.0 * 1024.0):0.00} GB" : "不可用 N/A";
    private static string FormatBps(double? b) => b.HasValue ? $"{b.Value / (1024.0 * 1024.0):0.00} MB/s" : "不可用 N/A";
    private static string FormatUptime(double? sec) => sec is double s && s >= 0 ? TimeSpan.FromSeconds(s).ToString(@"d\.hh\:mm") : "不可用 N/A";
    private static string LinkSpeed(long? bps) => bps switch
    {
        >= 1_000_000_000 => $"{bps.Value / 1_000_000_000.0:0.0} Gbps",
        >= 1_000_000 => $"{bps.Value / 1_000_000.0:0} Mbps",
        _ => "",
    };
}