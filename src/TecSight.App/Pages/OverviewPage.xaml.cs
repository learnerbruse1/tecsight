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
        var gpu = inv.Gpus.OrderByDescending(g => g.MemoryBytes ?? 0).FirstOrDefault();
        var gpuClock = m.Sensors.FirstOrDefault(s =>
            s.SensorName.Equals("GPU Core", StringComparison.OrdinalIgnoreCase) && s.Unit == "MHz")?.Value;
        var cpuSub = (cpu?.Name ?? loc["Common.NotAvailable"]) + (m.CpuFrequencyMhz.HasValue ? $"  ·  {Format.FreqGhz(m.CpuFrequencyMhz)}" : "");
        var gpuSub = (gpu?.Name ?? loc["Common.NotAvailable"]) + (gpuClock.HasValue ? $"  ·  {Format.FreqMhz(gpuClock)}" : "");
        var disk = inv.Disks.FirstOrDefault();
        var net = inv.NetworkAdapters.FirstOrDefault(n => n.IsPhysical == true) ?? inv.NetworkAdapters.FirstOrDefault();
        var bat = inv.Battery;

        // 关键温度 = 各温度传感器中的最高值；只取单位 °C 且落在合理范围（0–150°C），
        // 避免把 GPU 核心频率(MHz)/负载(%)等同名传感器误当作温度。
        var temps = m.Sensors
            .Where(s => s.Unit == "°C"
                        && (s.SensorName.Contains("CPU Package", StringComparison.OrdinalIgnoreCase)
                            || s.SensorName.Contains("GPU Core", StringComparison.OrdinalIgnoreCase)
                            || s.SensorName.Equals("Temperature", StringComparison.OrdinalIgnoreCase))
                        && s.Value is > 0 and < 150)
            .Select(s => s.Value!.Value)
            .ToList();
        double? keyTemp = temps.Count > 0 ? temps.Max() : null;

        var memSubtitle = m.MemoryTotalBytes.HasValue
            ? $"{Format.Bytes(m.MemoryTotalBytes)}  ({inv.MemoryModules.Count}×)"
            : loc["Common.NotAvailable"];

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
            new(loc["Overview.KeyTemp"], keyTemp.HasValue ? $"{keyTemp.Value:0.#} °C" : loc["Common.NotAvailable"]),
            new(loc["Overview.Motherboard"],
                inv.Motherboard is { } mb ? $"{mb.Manufacturer} {mb.Product}".Trim() : loc["Common.NotAvailable"],
                inv.Motherboard?.BiosVersion ?? loc["Common.NotAvailable"]),
            new(loc["Overview.System"], inv.OsCaption ?? loc["Common.NotAvailable"], inv.OsVersion ?? loc["Common.NotAvailable"]),
        };
    }

    private static string BatterySubtitle(BatteryInfo b, LocalizationManager loc)
    {
        if (b.FullChargeCapacityWh is double full && b.DesignedCapacityWh is double design && design > 0)
        {
            return $"{b.DeviceName}  {loc["Overview.BatteryHealth"]} {full / design * 100:0}%";
        }
        return b.DeviceName ?? loc["Common.NotAvailable"];
    }
}