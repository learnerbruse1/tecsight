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
        var disk = inv.Disks.FirstOrDefault();
        var net = inv.NetworkAdapters.FirstOrDefault(n => n.IsPhysical == true) ?? inv.NetworkAdapters.FirstOrDefault();
        var bat = inv.Battery;

        var temps = m.Sensors
            .Where(s => s.SensorName.Contains("CPU Package", StringComparison.OrdinalIgnoreCase)
                        || s.SensorName.Contains("GPU Core", StringComparison.OrdinalIgnoreCase)
                        || s.SensorName.Equals("Temperature", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .Where(v => v.HasValue)
            .ToList();
        var keyTemp = temps.Count > 0 ? temps.Max() : null;

        var memSubtitle = m.MemoryTotalBytes.HasValue
            ? $"{Format.Bytes(m.MemoryTotalBytes)}  ({inv.MemoryModules.Count}×)"
            : loc["Common.NotAvailable"];

        Cards.ItemsSource = new List<OverviewCard>
        {
            new(loc["Overview.Cpu"], Format.Pct(m.CpuUsagePercent), cpu?.Name ?? loc["Common.NotAvailable"]),
            new(loc["Overview.Memory"], $"{Format.Pct(m.MemoryUsagePercent)}  {Format.Bytes(m.MemoryUsedBytes)} / {Format.Bytes(m.MemoryTotalBytes)}", memSubtitle),
            new(loc["Overview.Disk"], $"{loc["Overview.Down"]} {Format.Bps(m.DiskReadBytesPerSec)}  {loc["Overview.Up"]} {Format.Bps(m.DiskWriteBytesPerSec)}",
                disk is null ? loc["Common.NotAvailable"] : $"{disk.Model}  {Format.Bytes(disk.CapacityBytes)}"),
            new(loc["Overview.Gpu"], Format.Pct(m.GpuUsagePercent), gpu?.Name ?? loc["Common.NotAvailable"]),
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