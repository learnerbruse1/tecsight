using System.Windows.Controls;
using TecSight.App.Models;

namespace TecSight.App.Pages;

public partial class OverviewPage : UserControl
{
    public OverviewPage() => InitializeComponent();

    public void Update(MainViewModel vm)
    {
        var m = vm.Snapshot.Metrics;
        var loc = vm.Loc;

        var temps = m.Sensors
            .Where(s => s.SensorName.Contains("CPU Package", StringComparison.OrdinalIgnoreCase)
                        || s.SensorName.Contains("GPU Core", StringComparison.OrdinalIgnoreCase)
                        || s.SensorName.Equals("Temperature", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value)
            .Where(v => v.HasValue)
            .ToList();
        var keyTemp = temps.Count > 0 ? temps.Max() : null;

        Cards.ItemsSource = new List<OverviewCard>
        {
            new(loc["Overview.Cpu"], Format.Pct(m.CpuUsagePercent)),
            new(loc["Overview.Memory"], $"{Format.Pct(m.MemoryUsagePercent)}   {Format.Bytes(m.MemoryUsedBytes)} / {Format.Bytes(m.MemoryTotalBytes)}"),
            new(loc["Overview.Disk"], $"{loc["Overview.Down"]} {Format.Bps(m.DiskReadBytesPerSec)}   {loc["Overview.Up"]} {Format.Bps(m.DiskWriteBytesPerSec)}"),
            new(loc["Overview.Gpu"], Format.Pct(m.GpuUsagePercent)),
            new(loc["Overview.Network"], $"{loc["Overview.Down"]} {Format.Bps(m.NetworkDownloadBps)}   {loc["Overview.Up"]} {Format.Bps(m.NetworkUploadBps)}"),
            new(loc["Overview.Battery"], $"{Format.Pct(m.BatteryChargePercent)} {(m.BatteryIsCharging == true ? "⚡" : "")}"),
            new(loc["Overview.KeyTemp"], keyTemp.HasValue ? $"{keyTemp.Value:0.#} °C" : loc["Common.NotAvailable"]),
        };
    }
}