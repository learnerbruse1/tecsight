using System.Globalization;
using System.Text;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>生成自包含的 HTML 可视化报告（浏览器可直接打开，便于分享）。</summary>
public static class HtmlReport
{
    public static string Build(Snapshot s)
    {
        var inv = s.Inventory;
        var m = s.Metrics;
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh\"><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>TecSight Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:'Segoe UI',system-ui,sans-serif;margin:24px;color:#1f2937;background:#f8fafc}");
        sb.AppendLine("h1{font-size:22px;margin:0 0 4px}h2{font-size:16px;margin:22px 0 8px;color:#1f2a44;border-bottom:2px solid #dbe3f0;padding-bottom:4px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;background:#fff;margin:6px 0 14px}");
        sb.AppendLine("th,td{border:1px solid #e2e8f0;padding:5px 9px;text-align:left;font-size:13px}");
        sb.AppendLine("th{background:#f1f5f9}.muted{color:#64748b;font-size:12px}.tag{display:inline-block;background:#e0e7ff;border-radius:10px;padding:1px 8px;font-size:12px;color:#3730a3}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>TecSight 硬件体检报告</h1>");
        sb.AppendLine($"<div class=\"muted\">生成时间：{s.CapturedAt:yyyy-MM-dd HH:mm:ss zzz} · {H(inv.ComputerName)} · {H(inv.OsCaption)} {H(inv.OsVersion)}</div>");

        sb.AppendLine("<h2>摘要</h2><table><tr><th>项目</th><th>值</th></tr>");
        Row(sb, "系统", $"{inv.OsCaption} {inv.OsVersion} {inv.OsArchitecture} {inv.FirmwareType}".Trim());
        Row(sb, "安装 / 启动", $"{inv.OsInstallDate} / {inv.LastBootTime}".Trim());
        Row(sb, "CPU", string.Join("; ", inv.Cpus.Select(c => $"{c.Name}（{c.CoreCount}核/{c.LogicalProcessorCount}线程）")));
        Row(sb, "内存", $"{inv.MemoryModules.Count} 条 / {Gb(inv.MemoryModules.Sum(x => long.TryParse(x.CapacityBytes, out var b) ? b : 0))}（使用 {Pct(m.MemoryUsagePercent)}）");
        Row(sb, "磁盘", string.Join("; ", inv.Disks.Select(d => $"{d.Model} {Gb(d.CapacityBytes)}（{d.MediaType} {d.BusType}）")));
        Row(sb, "显卡", string.Join("; ", inv.Gpus.Select(g => g.Name)));
        if (inv.Battery is { } bat)
            Row(sb, "电池", $"{bat.DeviceName} 设计 {Wh(bat.DesignedCapacityWh)} 满充 {Wh(bat.FullChargeCapacityWh)} 循环 {bat.CycleCount?.ToString() ?? "N/A"} 化学 {bat.Chemistry ?? "N/A"} 健康度 {HealthPct(bat)}");
        Row(sb, "运行时长", Up(m.SystemUptimeSeconds));
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>硬件清单</h2>");
        Section(sb, "CPU", ["型号", "核心", "线程", "架构", "插槽", "L2 缓存", "L3 缓存", "当前频率", "处理器 ID"],
            inv.Cpus.Select(c => new[] { c.Name, c.CoreCount.ToString(), c.LogicalProcessorCount.ToString(), c.Architecture, c.SocketDesignation, $"{c.L2CacheKb ?? 0} KB", $"{c.L3CacheKb ?? 0} KB", $"{c.CurrentClockMhz ?? 0} MHz", c.ProcessorId }).ToList());
        Section(sb, "内存模块", ["容量", "频率", "类型", "实际频率", "电压", "制造商", "序列号", "插槽"],
            inv.MemoryModules.Select(x => new[] { Gb(x.CapacityBytes), $"{x.Speed} MHz", x.MemoryType, x.ConfiguredClockMhz, x.ConfiguredVoltageMv, x.Manufacturer, x.SerialNumber, x.DeviceLocator }).ToList());
        Section(sb, "磁盘", ["型号", "容量", "介质", "总线", "固件", "健康度"],
            inv.Disks.Select(d => new[] { d.Model, Gb(d.CapacityBytes), d.MediaType, d.BusType, d.FirmwareVersion, Hlth(d.Health) }).ToList());
        Section(sb, "显卡", ["型号", "驱动"], inv.Gpus.Select(g => new[] { g.Name, g.DriverVersion }).ToList());
        if (inv.Motherboard is { } mb)
            Section(sb, "主板 / 系统", ["制造商", "型号", "BIOS", "系统制造商", "系统型号"],
                [new[] { mb.Manufacturer, mb.Product, mb.BiosVersion, mb.SystemManufacturer, mb.SystemModel }]);
        Section(sb, "网络适配器", ["名称", "MAC", "速率", "类型"],
            inv.NetworkAdapters.Select(n => new[] { n.Name, n.MacAddress, LinkSpeed(n.SpeedBps), n.AdapterType }).ToList());

        sb.AppendLine("<h2>运行指标</h2><table><tr><th>指标</th><th>值</th></tr>");
        Row(sb, "CPU 占用 / 频率", $"{Pct(m.CpuUsagePercent)} / {Mhz(m.CpuFrequencyMhz)}");
        Row(sb, "内存", $"{Pct(m.MemoryUsagePercent)}（{Gb(m.MemoryUsedBytes)} / {Gb(m.MemoryTotalBytes)}）");
        Row(sb, "磁盘 I/O", $"读 {Bps(m.DiskReadBytesPerSec)} / 写 {Bps(m.DiskWriteBytesPerSec)}");
        Row(sb, "网络", $"↓ {Bps(m.NetworkDownloadBps)} ↑ {Bps(m.NetworkUploadBps)}");
        Row(sb, "GPU", $"{Pct(m.GpuUsagePercent)}（{string.Join(", ", m.GpuEngines.Select(e => $"{e.EngineType} {e.Percent.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}%"))}）");
        Row(sb, "电池", $"{Pct(m.BatteryChargePercent)} {(m.BatteryIsCharging == true ? "充电中" : "")}");
        Row(sb, "进程", $"{m.Processes.Count} 个（共 {m.TotalProcessCount}）");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>其他设备</h2><table><tr><th>类别</th><th>数量</th><th>设备</th></tr>");
        Row(sb, "显示器", inv.Displays.Count.ToString(), string.Join("；", inv.Displays.Select(d => $"{d.Manufacturer} {d.Name}".Trim())));
        Row(sb, "音频", inv.AudioDevices.Count.ToString(), string.Join("；", inv.AudioDevices.Take(8).Select(a => a.Name)));
        Row(sb, "USB", inv.UsbDevices.Count.ToString(), string.Join("；", inv.UsbDevices.Take(8).Select(u => u.Name)));
        Row(sb, "键盘 / 鼠标", $"{inv.Keyboards.Count} / {inv.PointingDevices.Count}", "");
        Row(sb, "打印机", inv.Printers.Count.ToString(), string.Join("；", inv.Printers.Select(p => p.Name)));
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>传感器读数</h2><table><tr><th>硬件</th><th>传感器</th><th>值</th></tr>");
        foreach (var s2 in m.Sensors.Take(60))
            sb.AppendLine($"<tr><td>{H(s2.HardwareName)}</td><td>{H(s2.SensorName)}</td><td>{s2.Value?.ToString("0.#", CultureInfo.InvariantCulture) ?? "N/A"} {H(s2.Unit)}</td></tr>");
        if (m.Sensors.Count > 60) sb.AppendLine($"<tr><td colspan=\"3\">… 其余 {m.Sensors.Count - 60} 条</td></tr>");
        if (m.Sensors.Count == 0) sb.AppendLine("<tr><td colspan=\"3\">无传感器数据</td></tr>");
        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string title, string[] headers, List<string?[]> rows)
    {
        sb.AppendLine($"<h2>{title}</h2><table><tr>{string.Concat(headers.Select(h => $"<th>{H(h)}</th>"))}</tr>");
        foreach (var r in rows)
            sb.AppendLine($"<tr>{string.Concat(r.Select(c => $"<td>{H(c)}</td>"))}</tr>");
        sb.AppendLine("</table>");
    }

    private static void Row(StringBuilder sb, string k, string v) => sb.AppendLine($"<tr><td>{H(k)}</td><td>{H(v)}</td></tr>");
    private static void Row(StringBuilder sb, string k, string c, string v) => sb.AppendLine($"<tr><td>{H(k)}</td><td>{H(c)}</td><td>{H(v)}</td></tr>");

    private static string H(string? v) => (v ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    private static string Pct(double? v) => v.HasValue ? v.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%" : "N/A";
    private static string Mhz(double? v) => v.HasValue ? v.Value.ToString("0", CultureInfo.InvariantCulture) + " MHz" : "N/A";
    private static string Bps(double? v) => v.HasValue ? (v.Value / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture) + " MB/s" : "N/A";
    private static string Gb(double? b) => b.HasValue ? (b.Value / 1073741824.0).ToString("0.0", CultureInfo.InvariantCulture) + " GB" : "N/A";
    private static string Gb(long? b) => b.HasValue ? (b.Value / 1073741824.0).ToString("0.0", CultureInfo.InvariantCulture) + " GB" : "N/A";
    private static string Gb(string? s) => long.TryParse(s, out var v) ? (v / 1073741824.0).ToString("0.0", CultureInfo.InvariantCulture) + " GB" : "N/A";
    private static string Wh(double? v) => v.HasValue ? v.Value.ToString("0.0", CultureInfo.InvariantCulture) + " Wh" : "N/A";
    private static string Up(double? s) => s is double v ? TimeSpan.FromSeconds(v).ToString(@"d\.hh\:mm") : "N/A";
    private static string LinkSpeed(long? bps) => bps switch { >= 1_000_000_000 => (bps.Value / 1_000_000_000.0).ToString("0.0", CultureInfo.InvariantCulture) + " Gbps", >= 1_000_000 => (bps.Value / 1_000_000.0).ToString("0", CultureInfo.InvariantCulture) + " Mbps", _ => "" };
    private static string HealthPct(BatteryInfo b) => b.FullChargeCapacityWh is double f && b.DesignedCapacityWh is double d && d > 0 ? Math.Min(100, f / d * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%" : "N/A";
    private static string Hlth(StorageHealth? h) => h?.Status switch { HealthStatus.Good => "良好", HealthStatus.Warning => "注意", HealthStatus.Critical => "危险", _ => "N/A" };
}