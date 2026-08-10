using System.Windows.Controls;
using TecSight.App.Models;
using TecSight.Core.Models;

namespace TecSight.App.Pages;

public partial class DetailPage : UserControl
{
    private AppPage _category = AppPage.Cpu;
    private PageModel? _model;

    private sealed class PageModel
    {
        public required AppPage Category { get; init; }
        public required List<DetailSection> Sections { get; init; }
        public required List<LiveRow> MetricRows { get; init; }
        public required List<Func<MainViewModel, string>> MetricFormatters { get; init; }
        public required List<Func<LiveMetrics, double?>?> MetricSelectors { get; init; }
        public required Func<MainViewModel, IReadOnlyList<SensorReading>> SensorFilter { get; init; }
        public required List<LiveRow> SensorRows { get; init; }
        public required Func<MainViewModel, IReadOnlyList<SmartAttributeReading>> SmartFilter { get; init; }
        public required List<LiveRow> SmartRows { get; init; }
    }

    public DetailPage() => InitializeComponent();

    public void SetCategory(AppPage page) => _category = page;

    /// <summary>
    /// 每帧调用：只原地更新实时行的值/曲线，不重建控件树（修复滚动卡顿）。
    /// </summary>
    public void Update(MainViewModel vm)
    {
        var model = EnsureModel(vm);

        for (var i = 0; i < model.MetricRows.Count; i++)
        {
            model.MetricRows[i].Value = model.MetricFormatters[i](vm);
            var selector = model.MetricSelectors[i];
            model.MetricRows[i].Spark = selector is null ? null : Series(vm, selector);
        }

        var sensors = model.SensorFilter(vm);
        var sn = Math.Min(sensors.Count, model.SensorRows.Count);
        for (var i = 0; i < sn; i++)
        {
            model.SensorRows[i].Value = FormatSensorValue(sensors[i]);
        }

        var smart = model.SmartFilter(vm);
        var sm = Math.Min(smart.Count, model.SmartRows.Count);
        for (var i = 0; i < sm; i++)
        {
            model.SmartRows[i].Value = FormatSmartValue(smart[i]);
        }
    }

    private PageModel EnsureModel(MainViewModel vm)
    {
        var sensorCount = _model?.SensorFilter(vm).Count ?? 0;
        var smartCount = _model?.SmartFilter(vm).Count ?? 0;
        var needRebuild = _model is null
                          || _model.Category != _category
                          || vm.Snapshot.CapturedAt == DateTimeOffset.MinValue
                          || _model.SensorRows.Count != sensorCount
                          || _model.SmartRows.Count != smartCount;
        if (needRebuild)
        {
            _model = BuildModel(vm, _category);
            Sections.ItemsSource = _model.Sections;
        }
        return _model!;
    }

    private static PageModel BuildModel(MainViewModel vm, AppPage category)
    {
        var loc = vm.Loc;
        var inv = vm.Snapshot.Inventory;
        var sections = new List<DetailSection>();
        var metricRows = new List<LiveRow>();
        var formatters = new List<Func<MainViewModel, string>>();
        var selectors = new List<Func<LiveMetrics, double?>?>();
        var sensorRows = new List<LiveRow>();
        var smartRows = new List<LiveRow>();
        Func<MainViewModel, IReadOnlyList<SensorReading>>? sensorFilter = null;
        Func<MainViewModel, IReadOnlyList<SmartAttributeReading>>? smartFilter = null;

        switch (category)
        {
            case AppPage.Cpu:
            {
                var rows = new List<IDetailRow>();
                foreach (var c in inv.Cpus)
                {
                    rows.Add(new StaticRow(loc["Detail.Model"], c.Name ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Cores"], c.CoreCount.ToString()));
                    rows.Add(new StaticRow(loc["Detail.Threads"], c.LogicalProcessorCount.ToString()));
                    rows.Add(new StaticRow(loc["Detail.BaseClock"], c.BaseClockGhz.HasValue ? $"{c.BaseClockGhz.Value:0.0} GHz" : loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Manufacturer"], c.Manufacturer ?? loc["Common.NotAvailable"]));
                }
                sections.Add(new DetailSection(loc["Detail.Inventory"], rows));
                AddMetric(metricRows, formatters, selectors, loc["Detail.CpuUsage"],
                    v => Format.Pct(v.Snapshot.Metrics.CpuUsagePercent), m => m.CpuUsagePercent);
                sensorFilter = v => v.Snapshot.Metrics.Sensors.Where(s => MatchesCpu(s.HardwareName)).ToList();
                break;
            }
            case AppPage.Memory:
            {
                var rows = new List<IDetailRow>();
                foreach (var mm in inv.MemoryModules)
                {
                    rows.Add(new StaticRow(loc["Detail.Model"], $"{mm.Manufacturer ?? loc["Common.Unknown"]} {mm.PartNumber ?? ""}".Trim()));
                    rows.Add(new StaticRow(loc["Detail.Capacity"], $"{Format.Bytes(ParseBytes(mm.CapacityBytes))}  {mm.Speed ?? loc["Common.NotAvailable"]} MHz"));
                    rows.Add(new StaticRow(loc["Detail.Type"], mm.MemoryType ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.ConfigClock"], mm.ConfiguredClockMhz ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Voltage"], mm.ConfiguredVoltageMv ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Serial"], mm.SerialNumber ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Slot"], mm.DeviceLocator ?? loc["Common.NotAvailable"]));
                }
                if (rows.Count == 0) rows.Add(new StaticRow(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
                sections.Add(new DetailSection(loc["Detail.Inventory"], rows));
                AddMetric(metricRows, formatters, selectors, loc["Detail.MemUsage"],
                    v => Format.Pct(v.Snapshot.Metrics.MemoryUsagePercent), m => m.MemoryUsagePercent);
                AddMetric(metricRows, formatters, selectors, loc["Detail.MemUsed"],
                    v => $"{Format.Bytes(v.Snapshot.Metrics.MemoryUsedBytes)} / {Format.Bytes(v.Snapshot.Metrics.MemoryTotalBytes)}", null);
                break;
            }
            case AppPage.Disk:
            {
                var rows = new List<IDetailRow>();
                foreach (var d in inv.Disks)
                {
                    rows.Add(new StaticRow(loc["Detail.Model"], d.Model ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Capacity"], Format.Bytes(d.CapacityBytes)));
                    rows.Add(new StaticRow(loc["Detail.Serial"], d.SerialNumber ?? loc["Common.NotAvailable"]));
                }
                if (rows.Count == 0) rows.Add(new StaticRow(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
                sections.Add(new DetailSection(loc["Detail.Inventory"], rows));
                AddMetric(metricRows, formatters, selectors, loc["Detail.DiskRead"],
                    v => Format.Bps(v.Snapshot.Metrics.DiskReadBytesPerSec), m => m.DiskReadBytesPerSec);
                AddMetric(metricRows, formatters, selectors, loc["Detail.DiskWrite"],
                    v => Format.Bps(v.Snapshot.Metrics.DiskWriteBytesPerSec), m => m.DiskWriteBytesPerSec);
                sensorFilter = v => v.Snapshot.Metrics.Sensors.Where(s =>
                    s.SensorName.Contains("SMART", StringComparison.OrdinalIgnoreCase)
                    || s.SensorName.Contains("Remaining Life", StringComparison.OrdinalIgnoreCase)
                    || s.SensorName.Contains("Wear", StringComparison.OrdinalIgnoreCase)).ToList();
                smartFilter = v => v.Snapshot.Metrics.SmartAttributes;
                break;
            }
            case AppPage.Gpu:
            {
                var rows = new List<IDetailRow>();
                foreach (var g in inv.Gpus)
                {
                    rows.Add(new StaticRow(loc["Detail.Model"], g.Name ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Vram"], Format.Bytes(g.MemoryBytes)));
                    rows.Add(new StaticRow(loc["Detail.Driver"], g.DriverVersion ?? loc["Common.NotAvailable"]));
                }
                if (rows.Count == 0) rows.Add(new StaticRow(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
                sections.Add(new DetailSection(loc["Detail.Inventory"], rows));
                AddMetric(metricRows, formatters, selectors, loc["Detail.GpuUsage"],
                    v => Format.Pct(v.Snapshot.Metrics.GpuUsagePercent), m => m.GpuUsagePercent);
                // F6：GPU 引擎拆分 + GPU 相关传感器
                sensorFilter = v =>
                {
                    var list = v.Snapshot.Metrics.GpuEngines
                        .Select(e => new SensorReading("GPU Engine", e.EngineType, e.Percent, "%"))
                        .ToList();
                    list.AddRange(v.Snapshot.Metrics.Sensors.Where(s => MatchesGpu(s.HardwareName)));
                    return list;
                };
                break;
            }
            case AppPage.Motherboard:
            {
                var rows = new List<IDetailRow>
                {
                    new StaticRow(loc["Detail.Computer"], inv.ComputerName ?? loc["Common.NotAvailable"]),
                    new StaticRow(loc["Detail.Os"], $"{inv.OsCaption ?? ""} {inv.OsVersion ?? ""}".Trim()),
                };
                if (inv.Motherboard is { } mb)
                {
                    rows.Add(new StaticRow(loc["Detail.Manufacturer"], mb.Manufacturer ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Product"], mb.Product ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Bios"], mb.BiosVersion ?? loc["Common.NotAvailable"]));
                }
                sections.Add(new DetailSection(loc["Detail.Inventory"], rows));
                AddMetric(metricRows, formatters, selectors, loc["Detail.Uptime"],
                    v => Format.Uptime(v.Snapshot.Metrics.SystemUptimeSeconds, v.Loc.CurrentLanguage), null);
                break;
            }
            case AppPage.Network:
            {
                var rows = inv.NetworkAdapters
                    .Select(n => (IDetailRow)new StaticRow(n.Name ?? loc["Common.NotAvailable"],
                        $"{n.MacAddress ?? "—"}  {(n.IsPhysical == true ? loc["Detail.Yes"] : loc["Detail.No"])}"))
                    .ToList();
                if (rows.Count == 0) rows.Add(new StaticRow(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
                sections.Add(new DetailSection(loc["Detail.Inventory"], rows));
                AddMetric(metricRows, formatters, selectors, loc["Detail.NetDown"],
                    v => Format.Bps(v.Snapshot.Metrics.NetworkDownloadBps), m => m.NetworkDownloadBps);
                AddMetric(metricRows, formatters, selectors, loc["Detail.NetUp"],
                    v => Format.Bps(v.Snapshot.Metrics.NetworkUploadBps), m => m.NetworkUploadBps);
                // F10：IP / 网关 / DNS
                if (inv.NetworkConfigurations.Count > 0)
                {
                    var ipRows = inv.NetworkConfigurations
                        .Select(cfg => (IDetailRow)new StaticRow(cfg.Description ?? loc["Common.NotAvailable"],
                            $"{loc["Detail.Ip"]}: {Join(cfg.IpAddresses)}   {loc["Detail.Gateway"]}: {Join(cfg.Gateways)}   {loc["Detail.Dns"]}: {Join(cfg.DnsServers)}"))
                        .ToList();
                    sections.Add(new DetailSection(loc["Detail.Ip"], ipRows));
                }
                break;
            }
            case AppPage.Battery:
            {
                var rows = new List<IDetailRow>();
                if (inv.Battery is { } b)
                {
                    rows.Add(new StaticRow(loc["Detail.Model"], b.DeviceName ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.DesignCapacity"], b.DesignedCapacityWh.HasValue ? $"{b.DesignedCapacityWh.Value:0.0} Wh" : loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.FullChargeCapacity"], b.FullChargeCapacityWh.HasValue ? $"{b.FullChargeCapacityWh.Value:0.0} Wh" : loc["Common.NotAvailable"]));
                    if (b.FullChargeCapacityWh is double full && b.DesignedCapacityWh is double design && design > 0)
                    {
                        rows.Add(new StaticRow(loc["Detail.BatteryLoss"], $"{Math.Max(0, (1 - full / design) * 100):0.0}%"));
                        rows.Add(new StaticRow(loc["Detail.Health"], $"{full / design * 100:0.0}%"));
                    }
                }
                else
                {
                    rows.Add(new StaticRow(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
                }
                sections.Add(new DetailSection(loc["Detail.Inventory"], rows));
                AddMetric(metricRows, formatters, selectors, loc["Detail.BatteryLevel"],
                    v => $"{Format.Pct(v.Snapshot.Metrics.BatteryChargePercent)} {(v.Snapshot.Metrics.BatteryIsCharging == true ? "⚡" + v.Loc["Detail.Charging"] : "")}",
                    m => m.BatteryChargePercent);
                break;
            }
            case AppPage.Sensors:
                sensorFilter = v => v.Snapshot.Metrics.Sensors.ToList();
                break;
        }

        if (sensorFilter is not null)
        {
            foreach (var s in sensorFilter(vm))
            {
                sensorRows.Add(new LiveRow(FormatSensorLabel(s), FormatSensorValue(s)));
            }
            sections.Add(new DetailSection(loc["Detail.Sensors"], sensorRows.Cast<IDetailRow>().ToList()));
        }

        if (smartFilter is not null)
        {
            foreach (var a in smartFilter(vm))
            {
                smartRows.Add(new LiveRow(FormatSmartLabel(a), FormatSmartValue(a)));
            }
            sections.Add(new DetailSection(loc["Detail.Smart"], smartRows.Cast<IDetailRow>().ToList()));
        }

        return new PageModel
        {
            Category = category,
            Sections = sections,
            MetricRows = metricRows,
            MetricFormatters = formatters,
            MetricSelectors = selectors,
            SensorFilter = sensorFilter ?? (_ => []),
            SensorRows = sensorRows,
            SmartFilter = smartFilter ?? (_ => []),
            SmartRows = smartRows,
        };
    }

    private static void AddMetric(
        List<LiveRow> rows, List<Func<MainViewModel, string>> fmts, List<Func<LiveMetrics, double?>?> sels,
        string label, Func<MainViewModel, string> fmt, Func<LiveMetrics, double?>? sel)
    {
        rows.Add(new LiveRow(label));
        fmts.Add(fmt);
        sels.Add(sel);
    }

    private static List<double?> Series(MainViewModel vm, Func<LiveMetrics, double?> sel) => vm.History.Select(sel).ToList();

    private static string FormatSensorLabel(SensorReading s) => $"{s.HardwareName} / {s.SensorName}";

    private static string FormatSensorValue(SensorReading s) => $"{Format.Number(s.Value)} {s.Unit}".Trim();

    private static string FormatSmartLabel(SmartAttributeReading a) => $"{a.DiskName} / {a.Id:D2} {a.Name}";

    private static string FormatSmartValue(SmartAttributeReading a)
    {
        var parts = new List<string>();
        if (a.CurrentValue.HasValue) parts.Add($"V {a.CurrentValue.Value:0}");
        if (a.Worst.HasValue) parts.Add($"W {a.Worst.Value}");
        if (a.Threshold > 0) parts.Add($"T {a.Threshold}");
        if (!string.IsNullOrEmpty(a.RawValue)) parts.Add($"Raw {a.RawValue}");
        return string.Join("  ", parts);
    }

    private static string Join(IReadOnlyList<string> items) => items.Count > 0 ? string.Join(", ", items) : "—";

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
}