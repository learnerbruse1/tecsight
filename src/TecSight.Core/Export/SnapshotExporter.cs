using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>快照导出服务：把快照序列化为 JSON 或人类可读的 TXT。</summary>
public interface ISnapshotExporter
{
    string ExportJson(Snapshot snapshot);
    string ExportTxt(Snapshot snapshot);
    string ExportHtml(Snapshot snapshot);
    string ExportHistoryCsv(IEnumerable<LiveMetrics> history);
}

/// <summary>默认快照导出器。</summary>
public sealed class SnapshotExporter : ISnapshotExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

    public string ExportJson(Snapshot snapshot) => JsonSerializer.Serialize(snapshot, JsonOptions);

    public string ExportHtml(Snapshot snapshot) => HtmlReport.Build(snapshot);

    public string ExportHistoryCsv(IEnumerable<LiveMetrics> history)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,CpuPercent,CpuFrequencyMhz,MemoryPercent,MemoryUsedBytes,MemoryTotalBytes,GpuPercent,DiskReadBps,DiskWriteBps,NetDownBps,NetUpBps,BatteryPercent,UptimeSeconds");
        foreach (var m in history)
        {
            sb.AppendLine(string.Join(",",
                m.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Num(m.CpuUsagePercent), Num(m.CpuFrequencyMhz), Num(m.MemoryUsagePercent),
                Num(m.MemoryUsedBytes), Num(m.MemoryTotalBytes), Num(m.GpuUsagePercent),
                Num(m.DiskReadBytesPerSec), Num(m.DiskWriteBytesPerSec),
                Num(m.NetworkDownloadBps), Num(m.NetworkUploadBps),
                Num(m.BatteryChargePercent), Num(m.SystemUptimeSeconds)));
        }
        return sb.ToString();
    }

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
            sb.AppendLine($"  CPU: {c.Name ?? "不可用 N/A"}  {c.CoreCount}核/{c.LogicalProcessorCount}线程  {c.Architecture ?? ""}  L2={(c.L2CacheKb.HasValue ? $"{c.L2CacheKb}KB" : "N/A")} L3={(c.L3CacheKb.HasValue ? $"{c.L3CacheKb}KB" : "N/A")}  虚拟化 Virtualization: {YesNo(c.VirtualizationFirmwareEnabled)}".TrimEnd());
        }
        sb.AppendLine($"  内存 Memory: {inv.MemoryModules.Count} 条 / {FormatBytes(inv.MemoryModules.Sum(x => long.TryParse(x.CapacityBytes, out var b) ? b : 0))}");
        if (inv.MemoryTopology is { } mt)
        {
            sb.AppendLine($"  内存拓扑 Memory Topology: 插槽 Slots {mt.UsedSlots}/{mt.TotalSlots}  最大 Max {FormatBytes(mt.MaxCapacityBytes)}  ECC {mt.ErrorCorrection ?? "N/A"}".Trim());
        }
        foreach (var d in inv.Disks)
        {
            sb.AppendLine($"  磁盘 Disk: {d.Model ?? "不可用 N/A"}  {FormatBytes(d.CapacityBytes)}  {d.MediaType ?? ""}  {d.BusType ?? ""}  FW {d.FirmwareVersion ?? "N/A"}  健康度 Health: {HealthText(d.Health)}".TrimEnd());
        }
        foreach (var ld in inv.LogicalDisks)
        {
            sb.AppendLine($"  分区 Volume: {ld.DeviceId ?? "N/A"}  {ld.VolumeName ?? ""}  {ld.FileSystem ?? ""}  总 Total {FormatBytes(ld.TotalBytes)}  可用 Free {FormatBytes(ld.FreeBytes)}  {DriveTypeName(ld.DriveType)}".Trim());
        }
        sb.AppendLine($"  GPU: {string.Join("; ", inv.Gpus.Select(g => g.Name ?? "不可用 N/A"))}");
        if (inv.Motherboard is { } mb)
        {
            sb.AppendLine($"  主板 Motherboard: {mb.Manufacturer ?? "N/A"} {mb.Product ?? ""}  BIOS {mb.BiosVersion ?? "N/A"}  {mb.SystemManufacturer ?? ""} {mb.SystemModel ?? ""}".TrimEnd());
        }
        if (inv.Bios is { } bios)
        {
            sb.AppendLine($"  BIOS: {bios.Manufacturer ?? "N/A"} {bios.Name ?? ""} {bios.Version ?? ""} {bios.SmbiosVersion ?? ""} {bios.ReleaseDate ?? ""}  序列号 SN {bios.SerialNumber ?? "N/A"}".Trim());
        }
        if (inv.SystemDetails is { } sd)
        {
            sb.AppendLine($"  系统 System: 序列号 Serial {sd.SerialNumber ?? "N/A"}  UUID {sd.Uuid ?? "N/A"}  产品 Product {sd.ProductName ?? ""} {sd.ProductVersion ?? ""}  域 Domain {sd.Domain ?? "N/A"}  时区 TimeZone {sd.TimeZone ?? "N/A"}  安全启动 Secure Boot {YesNo(sd.SecureBoot)}  TPM {sd.TpmVersion ?? "N/A"}  VBS {VbsText(sd.VirtualizationBasedSecurityStatus)}  HVCI {YesNo(sd.MemoryIntegrityEnabled)}  Hypervisor {YesNo(sd.HypervisorPresent)}  类型 Type {sd.SystemType ?? "N/A"}".Trim());
        }
        foreach (var p in inv.ProblemDevices)
        {
            sb.AppendLine($"  问题设备 Problem: {p.Name ?? "N/A"}  错误 Error {p.ErrorCode?.ToString() ?? "?"}  {p.ErrorDescription ?? ""}  {p.Status ?? ""}".Trim());
        }
        foreach (var n in inv.NetworkAdapters)
        {
            sb.AppendLine($"  网卡 Network: {n.Name ?? "不可用 N/A"}  {n.MacAddress ?? "N/A"}  {LinkSpeed(n.SpeedBps)}  {n.AdapterType ?? ""}  {n.Manufacturer ?? ""}  驱动 Driver {n.DriverVersion ?? "N/A"} {n.DriverDate ?? ""}".TrimEnd());
        }
        foreach (var w in inv.WifiInterfaces)
        {
            sb.AppendLine($"  Wi-Fi: {w.Ssid ?? w.Name ?? "N/A"}  信号 Signal {w.SignalPercent?.ToString("0", CultureInfo.InvariantCulture) ?? "N/A"}%  信道 Channel {w.Channel?.ToString() ?? "N/A"}  {w.RadioType ?? ""}  {w.Authentication ?? ""}  收 Rx {w.ReceiveRateMbps?.ToString("0", CultureInfo.InvariantCulture) ?? "N/A"}Mbps  发 Tx {w.TransmitRateMbps?.ToString("0", CultureInfo.InvariantCulture) ?? "N/A"}Mbps".Trim());
        }
        if (inv.Battery is { } bat)
        {
            var health = bat.FullChargeCapacityWh is double full && bat.DesignedCapacityWh is double design && design > 0
                ? $"{(Math.Min(100, full / design * 100)).ToString("0.0", CultureInfo.InvariantCulture)}%"
                : "不可用 N/A";
            sb.AppendLine($"  电池 Battery: {bat.DeviceName ?? "不可用 N/A"}  设计 {bat.DesignedCapacityWh?.ToString("0.0", CultureInfo.InvariantCulture) ?? "N/A"}Wh  满充 {bat.FullChargeCapacityWh?.ToString("0.0", CultureInfo.InvariantCulture) ?? "N/A"}Wh  循环 {bat.CycleCount?.ToString() ?? "N/A"}  化学 {bat.Chemistry ?? "N/A"}  设计电压 {bat.DesignVoltageV?.ToString("0.00", CultureInfo.InvariantCulture) ?? "N/A"}V  当前电压 {bat.CurrentVoltageV?.ToString("0.00", CultureInfo.InvariantCulture) ?? "N/A"}V  健康度 {health}");
        }
        sb.AppendLine();
        sb.AppendLine("[其他设备 Other Devices]");
        sb.AppendLine($"  显示器 Displays: {inv.Displays.Count}  {JoinNames(inv.Displays.Select(d => $"{d.Manufacturer ?? ""} {d.Name ?? ""}".Trim()))}");
        sb.AppendLine($"  音频 Audio: {inv.AudioDevices.Count}  {JoinNames(inv.AudioDevices.Select(a => a.Name))}");
        sb.AppendLine($"  USB: {inv.UsbDevices.Count}  {JoinNames(inv.UsbDevices.Select(u => u.Name))}");
        sb.AppendLine($"  键盘 Keyboards: {inv.Keyboards.Count}  {JoinNames(inv.Keyboards.Select(k => k.Name))}");
        sb.AppendLine($"  鼠标 Mice: {inv.PointingDevices.Count}  {JoinNames(inv.PointingDevices.Select(m => m.Name))}");
        sb.AppendLine($"  打印机 Printers: {inv.Printers.Count}  {JoinNames(inv.Printers.Select(p => (p.IsDefault == true ? "[默认] " : "") + p.Name))}");
        sb.AppendLine();

        var m = snapshot.Metrics;
        sb.AppendLine("[运行指标 Live Metrics]");
        sb.AppendLine($"  CPU 占用 Usage: {FormatPct(m.CpuUsagePercent)}  频率 Freq: {FormatMhz(m.CpuFrequencyMhz)}  运行时长 Uptime: {FormatUptime(m.SystemUptimeSeconds)}");
        sb.AppendLine($"  内存 Memory: {FormatPct(m.MemoryUsagePercent)} ({FormatBytes(m.MemoryUsedBytes)} / {FormatBytes(m.MemoryTotalBytes)})");
        sb.AppendLine($"  GPU 占用 Usage: {FormatPct(m.GpuUsagePercent)}  引擎 Engines: {string.Join(", ", m.GpuEngines.Select(e => $"{e.EngineType} {e.Percent.ToString("0.0", CultureInfo.InvariantCulture)}%"))}");
        sb.AppendLine($"  磁盘 Disk: ↓读 {FormatBps(m.DiskReadBytesPerSec)} ↑写 {FormatBps(m.DiskWriteBytesPerSec)}");
        sb.AppendLine($"  网络 Network: ↓{FormatBps(m.NetworkDownloadBps)} ↑{FormatBps(m.NetworkUploadBps)}");
        sb.AppendLine($"  电池 Battery: {FormatPct(m.BatteryChargePercent)} {(m.BatteryIsCharging == true ? "充电 Charging" : "")}");
        sb.AppendLine($"  进程 Processes: {m.Processes.Count}  SMART 属性: {m.SmartAttributes.Count}  传感器 Sensors: {m.Sensors.Count}");
        sb.AppendLine();

        sb.AppendLine("[传感器读数 Sensor Readings]");
        foreach (var s in m.Sensors.Take(60))
        {
            sb.AppendLine($"  {s.HardwareName} / {s.SensorName}: {s.Value?.ToString("0.#", CultureInfo.InvariantCulture) ?? "不可用 N/A"} {s.Unit}");
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

    private static string JoinNames(IEnumerable<string?> names)
    {
        var list = names.Where(n => !string.IsNullOrWhiteSpace(n)).Take(8).Select(n => n!.Trim()).ToList();
        return list.Count == 0 ? "" : string.Join("; ", list) + (names.Count() > 8 ? " …" : "");
    }

    private static string Num(double? v) => v.HasValue ? v.Value.ToString("0.###", CultureInfo.InvariantCulture) : "";

    private static string HealthText(StorageHealth? h) => h?.Status switch
    {
        HealthStatus.Good => "良好 Good",
        HealthStatus.Warning => "注意 Warning",
        HealthStatus.Critical => "危险 Critical",
        _ => "不可用 N/A",
    };

    private static string FormatPct(double? v) => FormatUtil.Pct(v, "不可用 N/A");
    private static string FormatMhz(double? v) => FormatUtil.FreqMhz(v, "不可用 N/A");
    private static string FormatBytes(double? b) => FormatUtil.Bytes(b, "不可用 N/A");
    private static string FormatBps(double? b) => FormatUtil.Bps(b, "不可用 N/A");
    private static string FormatUptime(double? sec)
    {
        if (sec is not double s || s < 0) return "不可用 N/A";
        var t = TimeSpan.FromSeconds(s);
        return t.TotalDays >= 1 ? t.ToString(@"d\.hh\:mm") : t.ToString(@"hh\:mm");
    }
    private static string LinkSpeed(long? bps) => FormatUtil.LinkSpeed(bps, "");

    private static string YesNo(bool? b) => b == true ? "是 Yes" : b == false ? "否 No" : "N/A";

    private static string VbsText(int? s) => s switch { 0 => "Off", 1 => "Enabled", 2 => "Running", _ => "N/A" };

    private static string DriveTypeName(int? t) => t switch
    {
        2 => "可移动 Removable",
        3 => "本地磁盘 Fixed",
        4 => "网络 Network",
        5 => "光盘 Optical",
        6 => "内存盘 RAM Disk",
        _ => "未知 Unknown",
    };
}
