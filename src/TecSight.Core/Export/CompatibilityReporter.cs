using System.Text;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>
/// 兼容性自检报告（F15）：汇总各硬件类别与运行指标是否读到，便于在异机快速定位兼容性问题。
/// </summary>
public static class CompatibilityReporter
{
    public static string Build(Snapshot s)
    {
        var inv = s.Inventory;
        var m = s.Metrics;
        var sb = new StringBuilder();
        sb.AppendLine("TecSight Compatibility Report");
        sb.AppendLine($"Captured at: {s.CapturedAt:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();

        sb.AppendLine("== Hardware categories ==");
        Row(sb, "CPU", inv.Cpus.Count, inv.Cpus.FirstOrDefault()?.Name);
        Row(sb, "Memory", inv.MemoryModules.Count, $"{inv.MemoryModules.Count} module(s)");
        Row(sb, "Storage", inv.Disks.Count, inv.Disks.FirstOrDefault()?.Model);
        Row(sb, "GPU", inv.Gpus.Count, inv.Gpus.OrderByDescending(g => g.MemoryBytes ?? 0).FirstOrDefault()?.Name);
        Row(sb, "Motherboard", inv.Motherboard is null ? 0 : 1, inv.Motherboard?.Product);
        Row(sb, "Network adapters", inv.NetworkAdapters.Count, $"{inv.NetworkAdapters.Count} adapter(s)");
        Row(sb, "IP configurations", inv.NetworkConfigurations.Count, $"{inv.NetworkConfigurations.Count} enabled");
        Row(sb, "Battery", inv.Battery is null ? 0 : 1, inv.Battery?.DeviceName);
        sb.AppendLine();

        sb.AppendLine("== Live metrics ==");
        Row(sb, "CPU usage", m.CpuUsagePercent.HasValue ? 1 : 0, Pct(m.CpuUsagePercent));
        Row(sb, "Memory usage", m.MemoryUsagePercent.HasValue ? 1 : 0, Pct(m.MemoryUsagePercent));
        Row(sb, "GPU usage", m.GpuUsagePercent.HasValue ? 1 : 0, Pct(m.GpuUsagePercent));
        Row(sb, "Disk I/O", m.DiskReadBytesPerSec.HasValue ? 1 : 0, m.DiskReadBytesPerSec.HasValue ? "OK" : "N/A");
        Row(sb, "Network throughput", m.NetworkDownloadBps.HasValue ? 1 : 0, m.NetworkDownloadBps.HasValue ? "OK" : "N/A");
        Row(sb, "Battery charge", m.BatteryChargePercent.HasValue ? 1 : 0, Pct(m.BatteryChargePercent));
        sb.AppendLine();

        sb.AppendLine($"== Sensors: {m.Sensors.Count} total ==");
        foreach (var g in m.Sensors.GroupBy(x => x.HardwareName).OrderByDescending(x => x.Count()).Take(15))
        {
            sb.AppendLine($"  {g.Key}: {g.Count()} reading(s)");
        }
        sb.AppendLine();
        sb.AppendLine($"SMART attributes: {m.SmartAttributes.Count}");
        sb.AppendLine($"Process samples: {m.Processes.Count}");
        sb.AppendLine($"GPU engines: {string.Join(", ", m.GpuEngines.Select(e => $"{e.EngineType} {e.Percent:0.0}%"))}");

        return sb.ToString();
    }

    private static string? Pct(double? v) => v.HasValue ? v.Value.ToString("0.0") + "%" : "N/A";

    private static void Row(StringBuilder sb, string label, int found, string? detail)
        => sb.AppendLine($"  [{(found > 0 ? "OK" : "--")}] {label}: {detail ?? "not detected"}");
}