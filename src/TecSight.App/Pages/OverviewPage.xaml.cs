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

        var cpu = inv.Cpus.FirstOrDefault();
        var gpu = HardwareClassifier.PickPrimaryGpu(inv.Gpus);
        var disk = inv.Disks.FirstOrDefault();
        var net = inv.NetworkAdapters.FirstOrDefault(n => n.IsPhysical == true) ?? inv.NetworkAdapters.FirstOrDefault();
        var bat = inv.Battery;
        var gpuClock = m.Sensors.FirstOrDefault(s =>
            s.SensorName.Equals("GPU Core", StringComparison.OrdinalIgnoreCase) && s.Unit == "MHz")?.Value;
        var cpuSub = (cpu?.Name ?? loc["Common.NotAvailable"]) + (m.CpuFrequencyMhz.HasValue ? $"  ·  {Format.FreqGhz(m.CpuFrequencyMhz)}" : "");
        var gpuSub = (gpu?.Name ?? loc["Common.NotAvailable"]) + (gpuClock.HasValue ? $"  ·  {Format.FreqMhz(gpuClock)}" : "");

        var memSubtitle = m.MemoryTotalBytes.HasValue
            ? $"{Format.Bytes(m.MemoryTotalBytes)}  ({inv.MemoryModules.Count}×)"
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

        Cards.ItemsSource = new List<OverviewCard>
        {
            new(loc["Overview.Cpu"], Format.Pct(m.CpuUsagePercent), cpuSub),
            new(loc["Overview.Memory"], $"{Format.Pct(m.MemoryUsagePercent)}  {Format.Bytes(m.MemoryUsedBytes)} / {Format.Bytes(m.MemoryTotalBytes)}", memSubtitle),
            new(loc["Overview.Disk"], $"{loc["Overview.Down"]} {Format.Bps(m.DiskReadBytesPerSec)}  {loc["Overview.Up"]} {Format.Bps(m.DiskWriteBytesPerSec)}",
                disk is null ? loc["Common.NotAvailable"] : $"{disk.Model}  {Format.Bytes(disk.CapacityBytes)}"),
            new(loc["Overview.Gpu"], Format.Pct(m.GpuUsagePercent), gpuSub),
            new(loc["Overview.Network"], $"{loc["Overview.Down"]} {Format.Bps(m.NetworkDownloadBps)}  {loc["Overview.Up"]} {Format.Bps(m.NetworkUploadBps)}",
                net?.Name ?? loc["Common.NotAvailable"]),
            new(loc["Overview.Battery"], $"{Format.Pct(m.BatteryChargePercent)} {(m.BatteryIsCharging == true ? "⚡" : "")}",
                bat is null ? loc["Common.NotAvailable"] : BatterySubtitle(bat, loc)),
            new(loc["Overview.CpuTemp"], cpuTemp.HasValue ? $"{cpuTemp.Value:0.#} °C" : loc["Common.NotAvailable"]),
            new(loc["Overview.GpuTemp"], gpuTemp.HasValue ? $"{gpuTemp.Value:0.#} °C" : loc["Common.NotAvailable"]),
            new(loc["Overview.Fan"], fanRpm.HasValue ? $"{fanRpm.Value:0} RPM" : loc["Common.NotAvailable"]),
            new(loc["Overview.Uptime"], Format.Uptime(m.SystemUptimeSeconds, loc.CurrentLanguage)),
            new(loc["Overview.Motherboard"],
                inv.Motherboard is { } mb ? $"{mb.Manufacturer} {mb.Product}".Trim() : loc["Common.NotAvailable"],
                inv.Motherboard?.BiosVersion ?? loc["Common.NotAvailable"]),
            new(loc["Overview.System"], inv.OsCaption ?? loc["Common.NotAvailable"], inv.OsVersion ?? loc["Common.NotAvailable"]),
        };
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
            text += $"  {loc["Overview.BatteryHealth"]} {Math.Min(100, full / design * 100):0}%";
        }
        return text;
    }
}