using System.Globalization;
using System.Windows.Controls;
using TecSight.App.Localization;
using TecSight.App.Models;
using TecSight.Core.Models;

namespace TecSight.App.Pages;

public partial class OverviewPage : UserControl
{
    public OverviewPage() => InitializeComponent();

    public void Update(MainViewModel vm)
    {
        var m = vm.Snapshot.Metrics;
        var inv = vm.Snapshot.Inventory;
        var loc = vm.Loc;

        UpdatedText.Text = m.Timestamp == DateTimeOffset.MinValue
            ? $"{loc["Overview.UpdatedAt"]} {loc["Common.NotAvailable"]}"
            : $"{loc["Overview.UpdatedAt"]} {m.Timestamp:HH:mm:ss}";
        var interval = AppSettings.RefreshIntervalSeconds;
        RefreshText.Text = loc.CurrentLanguage == "zh"
            ? $"{loc["Overview.Refreshing"]}（{interval:0.#} 秒）"
            : $"{loc["Overview.Refreshing"]} ({interval:0.#}s)";

        var cpu = inv.Cpus.FirstOrDefault();
        var gpu = HardwareClassifier.PickPrimaryGpu(inv.Gpus);
        var disk = inv.Disks.FirstOrDefault();
        var net = PickPrimaryNetwork(inv.NetworkAdapters);
        var netSub = net?.Name ?? loc["Common.NotAvailable"];
        if (net?.SpeedBps is long sp && sp > 0)
        {
            netSub += $"  ·  {Format.LinkSpeed(sp)}";
        }
        var bat = inv.Battery;
        var gpuClock = m.Sensors.FirstOrDefault(s =>
            s.SensorName.Equals("GPU Core", StringComparison.OrdinalIgnoreCase) && s.Unit == "MHz")?.Value;
        var cpuSub = (cpu?.Name ?? loc["Common.NotAvailable"]) + (m.CpuFrequencyMhz.HasValue ? $"  ·  {Format.FreqGhz(m.CpuFrequencyMhz)}" : "");
        var gpuSub = (gpu?.Name ?? loc["Common.NotAvailable"]) + (gpuClock.HasValue ? $"  ·  {Format.FreqMhz(gpuClock)}" : "");

        var memType = inv.MemoryModules.FirstOrDefault()?.MemoryType;
        var memDetail = inv.MemoryModules.Count > 0
            ? $"{inv.MemoryModules.Count}×" + (string.IsNullOrEmpty(memType) ? "" : " " + memType)
            : null;
        var memSubtitle = m.MemoryTotalBytes.HasValue
            ? $"{Format.Bytes(m.MemoryTotalBytes)}{(memDetail is null ? "" : $"  ({memDetail.Trim()})")}"
            : loc["Common.NotAvailable"];

        var cpuTemp = PreferNamedTemp(m.Sensors, HardwareClassifier.MatchesCpuHw, "CPU Package");
        var gpuTemp = PreferNamedTemp(m.Sensors, HardwareClassifier.MatchesGpuHw, "GPU Core");
        var fanVals = m.Sensors
            .Where(s => s.Unit == "RPM" || s.SensorName.Contains("Fan", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .Where(v => v is > 0)
            .Select(v => v!.Value)
            .ToList();
        double? fanRpm = fanVals.Count > 0 ? fanVals.Max() : null;

        // 核心指标/硬件清单整体缺失 → 提示数据源异常（性能计数器/WMI 不可用等）
        var metricsMissing = !m.CpuUsagePercent.HasValue && !m.MemoryUsagePercent.HasValue && !m.GpuUsagePercent.HasValue;
        var inventoryEmpty = inv.Cpus.Count == 0 && inv.MemoryModules.Count == 0 && inv.Disks.Count == 0 && inv.Gpus.Count == 0;
        WarnText.Text = metricsMissing && inventoryEmpty
            ? loc["Overview.CollectFailed"] + "  " + loc["Overview.InventoryFailed"]
            : metricsMissing ? loc["Overview.CollectFailed"]
            : inventoryEmpty ? loc["Overview.InventoryFailed"]
            : "";
        WarnText.Visibility = (metricsMissing || inventoryEmpty) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        Cards.ItemsSource = new List<OverviewCard>
        {
            new(loc["Overview.Cpu"], Format.Pct(m.CpuUsagePercent), cpuSub),
            new(loc["Overview.Memory"], $"{Format.Pct(m.MemoryUsagePercent)}  {Format.Bytes(m.MemoryUsedBytes)} / {Format.Bytes(m.MemoryTotalBytes)}", memSubtitle),
            new(loc["Overview.Disk"], $"{loc["Overview.Down"]} {Format.Bps(m.DiskReadBytesPerSec)}  {loc["Overview.Up"]} {Format.Bps(m.DiskWriteBytesPerSec)}",
                disk is null ? loc["Common.NotAvailable"] : $"{disk.Model}  {Format.Bytes(disk.CapacityBytes)}"),
            new(loc["Overview.Gpu"], Format.Pct(m.GpuUsagePercent), gpuSub),
            new(loc["Overview.Network"], $"{loc["Overview.Down"]} {Format.Bps(m.NetworkDownloadBps)}  {loc["Overview.Up"]} {Format.Bps(m.NetworkUploadBps)}",
                netSub),
            new(loc["Overview.Battery"], $"{Format.Pct(m.BatteryChargePercent)} {(m.BatteryIsCharging == true ? "⚡" : "")}",
                bat is null ? loc["Common.NotAvailable"] : BatterySubtitle(bat, loc)),
            new(loc["Overview.CpuTemp"], cpuTemp.HasValue ? cpuTemp.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " °C" : loc["Common.NotAvailable"]),
            new(loc["Overview.GpuTemp"], gpuTemp.HasValue ? gpuTemp.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " °C" : loc["Common.NotAvailable"]),
            new(loc["Overview.Fan"], fanRpm.HasValue ? $"{fanRpm.Value.ToString("0", CultureInfo.InvariantCulture)} RPM" : loc["Common.NotAvailable"]),
            new(loc["Overview.Uptime"], Format.Uptime(m.SystemUptimeSeconds, loc.CurrentLanguage)),
            new(loc["Overview.Motherboard"],
                inv.Motherboard is { } mb ? $"{mb.Manufacturer} {mb.Product}".Trim() : loc["Common.NotAvailable"],
                inv.Motherboard?.BiosVersion ?? loc["Common.NotAvailable"]),
            new(loc["Overview.System"], inv.OsCaption ?? loc["Common.NotAvailable"], inv.OsVersion ?? loc["Common.NotAvailable"]),
        };
    }


    /// <summary>挑选概览页主网卡：优先真实有线/无线网卡，排除 TAP/隧道/虚拟等。</summary>
    private static NetworkAdapterInfo? PickPrimaryNetwork(IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        if (adapters.Count == 0) return null;
        var real = adapters
            .Where(a => !HardwareClassifier.IsVirtualNetworkAdapter(a.Name, a.AdapterType))
            .ToList();
        var candidates = real.Count > 0 ? real : adapters;
        return candidates.OrderByDescending(ScoreNetworkAdapter).First();
    }

    private static int ScoreNetworkAdapter(NetworkAdapterInfo a)
    {
        var score = 0;
        if (a.IsPhysical == true) score += 100;
        if (!string.IsNullOrWhiteSpace(a.MacAddress)) score += 40;
        if (a.SpeedBps is long sp && sp > 0) score += 20;
        if (HardwareClassifier.IsVirtualNetworkAdapter(a.Name, a.AdapterType)) score -= 200;
        return score;
    }

    /// <summary>取硬件温度：优先指定名称（如 GPU Core），否则取该硬件最高温度。</summary>
    private static double? PreferNamedTemp(IEnumerable<SensorReading> sensors, Func<string, bool> hwMatch, string preferred)
    {
        var vals = sensors
            .Where(s => s.Unit == "°C" && hwMatch(s.HardwareName) && s.Value is > 0 and < 150)
            .Select(s => (Name: s.SensorName, V: s.Value!.Value))
            .ToList();
        if (vals.Count == 0) return null;
        var pref = vals.Where(x => x.Name.Contains(preferred, StringComparison.OrdinalIgnoreCase)).Select(x => x.V).ToList();
        return (pref.Count > 0 ? pref : vals.Select(x => x.V)).Max();
    }

    private static string BatterySubtitle(BatteryInfo b, LocalizationManager loc)
    {
        var text = b.DeviceName ?? loc["Common.NotAvailable"];
        if (b.FullChargeCapacityWh is double full && b.DesignedCapacityWh is double design && design > 0)
        {
            text += $"  {loc["Overview.BatteryHealth"]} {Math.Min(100, full / design * 100).ToString("0", CultureInfo.InvariantCulture)}%";
        }
        return text;
    }
}
