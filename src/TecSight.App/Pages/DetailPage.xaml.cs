using System.Windows.Controls;
using TecSight.App.Models;
using TecSight.Core.Models;

namespace TecSight.App.Pages;

public partial class DetailPage : UserControl
{
    private AppPage _category = AppPage.Cpu;

    public DetailPage() => InitializeComponent();

    public void SetCategory(AppPage page) => _category = page;

    public void Update(MainViewModel vm)
    {
        var sections = _category switch
        {
            AppPage.Cpu => BuildCpu(vm),
            AppPage.Memory => BuildMemory(vm),
            AppPage.Disk => BuildDisk(vm),
            AppPage.Gpu => BuildGpu(vm),
            AppPage.Motherboard => BuildMotherboard(vm),
            AppPage.Network => BuildNetwork(vm),
            AppPage.Battery => BuildBattery(vm),
            AppPage.Sensors => BuildSensors(vm),
            _ => [],
        };
        Sections.ItemsSource = sections;
    }

    // ---- builders ----
    private List<DetailSection> BuildCpu(MainViewModel vm)
    {
        var loc = vm.Loc;
        var inv = vm.Snapshot.Inventory;
        var m = vm.Snapshot.Metrics;

        var invRows = new List<DetailRow>();
        foreach (var c in inv.Cpus)
        {
            invRows.Add(Row(loc["Detail.Model"], c.Name ?? loc["Common.NotAvailable"]));
            invRows.Add(Row(loc["Detail.Cores"], c.CoreCount.ToString()));
            invRows.Add(Row(loc["Detail.Threads"], c.LogicalProcessorCount.ToString()));
            invRows.Add(Row(loc["Detail.BaseClock"], c.BaseClockGhz.HasValue ? $"{c.BaseClockGhz.Value:0.0} GHz" : loc["Common.NotAvailable"]));
            invRows.Add(Row(loc["Detail.Manufacturer"], c.Manufacturer ?? loc["Common.NotAvailable"]));
        }

        var liveRows = new List<DetailRow>
        {
            Row(loc["Detail.CpuUsage"], Format.Pct(m.CpuUsagePercent), Series(vm, x => x.CpuUsagePercent)),
        };

        var sensorRows = SensorsWhere(vm, s => MatchesCpu(s.HardwareName));

        return [Section(loc["Detail.Inventory"], invRows), Section(loc["Detail.Live"], liveRows), Section(loc["Detail.Sensors"], sensorRows)];
    }

    private List<DetailSection> BuildMemory(MainViewModel vm)
    {
        var loc = vm.Loc;
        var m = vm.Snapshot.Metrics;
        var invRows = vm.Snapshot.Inventory.MemoryModules
            .Select(mm => Row($"{mm.Manufacturer ?? loc["Common.Unknown"]} {mm.PartNumber ?? ""}".Trim(), $"{Format.Bytes(ParseBytes(mm.CapacityBytes))}  {mm.Speed ?? "?"} MHz"))
            .ToList();
        if (invRows.Count == 0) invRows.Add(Row(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
        var liveRows = new List<DetailRow>
        {
            Row(loc["Detail.MemUsage"], Format.Pct(m.MemoryUsagePercent), Series(vm, x => x.MemoryUsagePercent)),
            Row(loc["Detail.MemUsed"], $"{Format.Bytes(m.MemoryUsedBytes)} / {Format.Bytes(m.MemoryTotalBytes)}"),
        };
        return [Section(loc["Detail.Inventory"], invRows), Section(loc["Detail.Live"], liveRows)];
    }

    private List<DetailSection> BuildDisk(MainViewModel vm)
    {
        var loc = vm.Loc;
        var m = vm.Snapshot.Metrics;
        var invRows = new List<DetailRow>();
        foreach (var d in vm.Snapshot.Inventory.Disks)
        {
            invRows.Add(Row(loc["Detail.Model"], d.Model ?? loc["Common.NotAvailable"]));
            invRows.Add(Row(loc["Detail.Capacity"], Format.Bytes(d.CapacityBytes)));
            invRows.Add(Row(loc["Detail.Serial"], d.SerialNumber ?? loc["Common.NotAvailable"]));
        }
        if (invRows.Count == 0) invRows.Add(Row(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));

        var liveRows = new List<DetailRow>
        {
            Row(loc["Detail.DiskRead"], Format.Bps(m.DiskReadBytesPerSec), Series(vm, x => x.DiskReadBytesPerSec)),
            Row(loc["Detail.DiskWrite"], Format.Bps(m.DiskWriteBytesPerSec), Series(vm, x => x.DiskWriteBytesPerSec)),
        };

        var smartRows = SensorsWhere(vm, s => s.SensorName.Contains("SMART", StringComparison.OrdinalIgnoreCase)
                                              || s.SensorName.Contains("Remaining Life", StringComparison.OrdinalIgnoreCase)
                                              || s.SensorName.Contains("Wear", StringComparison.OrdinalIgnoreCase));
        return [Section(loc["Detail.Inventory"], invRows), Section(loc["Detail.Live"], liveRows), Section(loc["Detail.Sensors"], smartRows)];
    }

    private List<DetailSection> BuildGpu(MainViewModel vm)
    {
        var loc = vm.Loc;
        var m = vm.Snapshot.Metrics;
        var invRows = new List<DetailRow>();
        foreach (var g in vm.Snapshot.Inventory.Gpus)
        {
            invRows.Add(Row(loc["Detail.Model"], g.Name ?? loc["Common.NotAvailable"]));
            invRows.Add(Row(loc["Detail.Vram"], Format.Bytes(g.MemoryBytes)));
            invRows.Add(Row(loc["Detail.Driver"], g.DriverVersion ?? loc["Common.NotAvailable"]));
        }
        if (invRows.Count == 0) invRows.Add(Row(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
        var liveRows = new List<DetailRow>
        {
            Row(loc["Detail.GpuUsage"], Format.Pct(m.GpuUsagePercent), Series(vm, x => x.GpuUsagePercent)),
        };
        var sensorRows = SensorsWhere(vm, s => MatchesGpu(s.HardwareName));
        return [Section(loc["Detail.Inventory"], invRows), Section(loc["Detail.Live"], liveRows), Section(loc["Detail.Sensors"], sensorRows)];
    }

    private List<DetailSection> BuildMotherboard(MainViewModel vm)
    {
        var loc = vm.Loc;
        var inv = vm.Snapshot.Inventory;
        var rows = new List<DetailRow>
        {
            Row(loc["Detail.Computer"], inv.ComputerName ?? loc["Common.NotAvailable"]),
            Row(loc["Detail.Os"], $"{inv.OsCaption ?? ""} {inv.OsVersion ?? ""}".Trim()),
        };
        if (inv.Motherboard is { } mb)
        {
            rows.Add(Row(loc["Detail.Manufacturer"], mb.Manufacturer ?? loc["Common.NotAvailable"]));
            rows.Add(Row(loc["Detail.Product"], mb.Product ?? loc["Common.NotAvailable"]));
            rows.Add(Row(loc["Detail.Bios"], mb.BiosVersion ?? loc["Common.NotAvailable"]));
        }
        return [Section(loc["Detail.Inventory"], rows)];
    }

    private List<DetailSection> BuildNetwork(MainViewModel vm)
    {
        var loc = vm.Loc;
        var m = vm.Snapshot.Metrics;
        var invRows = vm.Snapshot.Inventory.NetworkAdapters
            .Select(n => Row(n.Name ?? "?", $"{n.MacAddress ?? "—"}  {(n.IsPhysical == true ? loc["Detail.Yes"] : loc["Detail.No"])}"))
            .ToList();
        if (invRows.Count == 0) invRows.Add(Row(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
        var liveRows = new List<DetailRow>
        {
            Row(loc["Detail.NetDown"], Format.Bps(m.NetworkDownloadBps), Series(vm, x => x.NetworkDownloadBps)),
            Row(loc["Detail.NetUp"], Format.Bps(m.NetworkUploadBps), Series(vm, x => x.NetworkUploadBps)),
        };
        return [Section(loc["Detail.Inventory"], invRows), Section(loc["Detail.Live"], liveRows)];
    }

    private List<DetailSection> BuildBattery(MainViewModel vm)
    {
        var loc = vm.Loc;
        var m = vm.Snapshot.Metrics;
        var invRows = new List<DetailRow>();
        if (vm.Snapshot.Inventory.Battery is { } b)
        {
            invRows.Add(Row(loc["Detail.Model"], b.DeviceName ?? loc["Common.NotAvailable"]));
        }
        else
        {
            invRows.Add(Row(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
        }
        var liveRows = new List<DetailRow>
        {
            Row(loc["Detail.BatteryLevel"], $"{Format.Pct(m.BatteryChargePercent)} {(m.BatteryIsCharging == true ? "⚡" + loc["Detail.Charging"] : "")}",
                Series(vm, x => x.BatteryChargePercent)),
        };
        return [Section(loc["Detail.Inventory"], invRows), Section(loc["Detail.Live"], liveRows)];
    }

    private List<DetailSection> BuildSensors(MainViewModel vm)
    {
        var loc = vm.Loc;
        var rows = vm.Snapshot.Metrics.Sensors
            .GroupBy(s => s.HardwareName)
            .SelectMany(g => g.Select(s => Row($"{g.Key} / {s.SensorName}", $"{Format.Number(s.Value)} {s.Unit}")) )
            .ToList();
        if (rows.Count == 0) rows.Add(Row(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
        return [Section(loc["Detail.Sensors"], rows)];
    }

    // ---- helpers ----
    private static List<DetailRow> SensorsWhere(MainViewModel vm, Func<SensorReading, bool> pred) =>
        vm.Snapshot.Metrics.Sensors
            .Where(pred)
            .Select(s => Row($"{s.HardwareName} / {s.SensorName}", $"{Format.Number(s.Value)} {s.Unit}".Trim()))
            .ToList();

    private static List<double?> Series(MainViewModel vm, Func<LiveMetrics, double?> sel) => vm.History.Select(sel).ToList();

    private static bool MatchesCpu(string name) =>
        name.Contains("CPU", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Core", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Package", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Intel", StringComparison.OrdinalIgnoreCase)
        || name.Contains("AMD", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesGpu(string name) =>
        name.Contains("GPU", StringComparison.OrdinalIgnoreCase)
        || name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
        || name.Contains("RTX", StringComparison.OrdinalIgnoreCase)
        || name.Contains("GTX", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Graphics", StringComparison.OrdinalIgnoreCase);

    private static double? ParseBytes(string? s) => long.TryParse(s, out var b) ? b : null;

    private static DetailRow Row(string label, string value, IReadOnlyList<double?>? spark = null) => new(label, value, spark);

    private static DetailSection Section(string title, IReadOnlyList<DetailRow> rows) => new(title, rows);
}