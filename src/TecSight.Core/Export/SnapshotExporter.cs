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
        sb.AppendLine($"  系统 OS: {inv.OsCaption ?? "不可用 N/A"} {inv.OsVersion ?? ""}".TrimEnd());
        sb.AppendLine($"  CPU: {string.Join("; ", inv.Cpus.Select(c => c.Name ?? "不可用 N/A"))}");
        sb.AppendLine($"  内存 Memory: {inv.MemoryModules.Count} 条 / {FormatBytes(inv.MemoryModules.Sum(m => long.TryParse(m.CapacityBytes, out var b) ? b : 0))}");
        foreach (var d in inv.Disks)
        {
            sb.AppendLine($"  磁盘 Disk: {d.Model ?? "不可用 N/A"} 健康度 Health: {d.Health?.Status ?? HealthStatus.Unknown}");
        }
        sb.AppendLine($"  GPU: {string.Join("; ", inv.Gpus.Select(g => g.Name ?? "不可用 N/A"))}");
        if (inv.Motherboard is { } mb)
        {
            sb.AppendLine($"  主板 Motherboard: {mb.Manufacturer ?? "N/A"} {mb.Product ?? ""}".TrimEnd());
        }
        foreach (var n in inv.NetworkAdapters)
        {
            sb.AppendLine($"  网卡 Network: {n.Name ?? "不可用 N/A"} ({n.MacAddress ?? "N/A"})");
        }
        if (inv.Battery is { } bat)
        {
            sb.AppendLine($"  电池 Battery: {bat.DeviceName ?? "不可用 N/A"}");
        }
        sb.AppendLine();

        var m = snapshot.Metrics;
        sb.AppendLine("[运行指标 Live Metrics]");
        sb.AppendLine($"  CPU 占用 Usage: {FormatPct(m.CpuUsagePercent)}");
        sb.AppendLine($"  内存 Memory: {FormatPct(m.MemoryUsagePercent)} ({FormatBytes(m.MemoryUsedBytes)} / {FormatBytes(m.MemoryTotalBytes)})");
        sb.AppendLine($"  GPU 占用 Usage: {FormatPct(m.GpuUsagePercent)}");
        sb.AppendLine($"  网络 Network: ↓{FormatBps(m.NetworkDownloadBps)} ↑{FormatBps(m.NetworkUploadBps)}");
        sb.AppendLine($"  电池 Battery: {FormatPct(m.BatteryChargePercent)} {(m.BatteryIsCharging == true ? "充电 Charging" : "")}");
        sb.AppendLine();

        sb.AppendLine("[传感器读数 Sensor Readings]");
        foreach (var s in m.Sensors)
        {
            sb.AppendLine($"  {s.HardwareName} / {s.SensorName}: {s.Value?.ToString("0.#") ?? "不可用 N/A"} {s.Unit}");
        }
        if (m.Sensors.Count == 0)
        {
            sb.AppendLine("  （无传感器数据 No sensor data）");
        }

        return sb.ToString();
    }

    private static string FormatPct(double? v) => v.HasValue ? $"{v.Value:0.0}%" : "不可用 N/A";
    private static string FormatBytes(double? b) => b.HasValue ? $"{b.Value / (1024.0 * 1024.0 * 1024.0):0.00} GB" : "不可用 N/A";
    private static string FormatBps(double? b) => b.HasValue ? $"{b.Value / (1024.0 * 1024.0):0.00} MB/s" : "不可用 N/A";
}