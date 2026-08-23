using System.Globalization;
using System.Windows.Controls;
using TecSight.App.Localization;
using TecSight.App.Models;
using TecSight.Core.Models;

namespace TecSight.App.Pages;

public partial class DetailPage : UserControl
{
    private AppPage _category = AppPage.Cpu;
    private PageModel? _model;
    private MainViewModel? _lastVm;
    private bool _hideNetworkNoise; // 传感器页：隐藏网络过滤器噪音（默认关闭，不改变默认行为）

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
        public required DateTimeOffset BuiltCapturedAt { get; init; }
    }

    public DetailPage() => InitializeComponent();

    public void SetCategory(AppPage page) => _category = page;

    /// <summary>语言/主题切换后调用：使缓存的页面模型失效，下次更新时按新语言重建。</summary>
    public void InvalidateModel() => _model = null;

    /// <summary>应用启动时恢复「隐藏网络过滤器噪音」偏好。</summary>
    public void SetHideNetworkNoise(bool value)
    {
        _hideNetworkNoise = value;
        NoiseFilterBox.IsChecked = value;
    }

    /// <summary>
    /// 每帧调用：只原地更新实时行的值/曲线，不重建控件树（修复滚动卡顿）。
    /// </summary>
    public void Update(MainViewModel vm)
    {
        _lastVm = vm;
        NoiseFilterBox.Visibility = _category == AppPage.Sensors ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
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
                          || (_model.BuiltCapturedAt == DateTimeOffset.MinValue && vm.Snapshot.CapturedAt != DateTimeOffset.MinValue)
                          || _model.SensorRows.Count != sensorCount
                          || _model.SmartRows.Count != smartCount;
        if (needRebuild)
        {
            _model = BuildModel(vm, _category, _hideNetworkNoise);
            Sections.ItemsSource = _model.Sections;
        }
        return _model!;
    }

    private static PageModel BuildModel(MainViewModel vm, AppPage category, bool hideNetworkNoise)
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
                for (var i = 0; i < inv.Cpus.Count; i++)
                {
                    var c = inv.Cpus[i];
                    var rows = new List<IDetailRow>
                    {
                        new StaticRow(loc["Detail.PhysicalCpuCount"], inv.Cpus.Count.ToString()),
                    };
                    rows.Add(new StaticRow(loc["Detail.Model"], c.Name ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Cores"], c.CoreCount.ToString()));
                    rows.Add(new StaticRow(loc["Detail.Threads"], c.LogicalProcessorCount.ToString()));
                    rows.Add(new StaticRow(loc["Detail.BaseClock"], c.BaseClockGhz.HasValue ? $"{c.BaseClockGhz.Value.ToString("0.0", CultureInfo.InvariantCulture)} GHz" : loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.CurrentClock"], c.CurrentClockMhz.HasValue ? $"{c.CurrentClockMhz.Value.ToString("0", CultureInfo.InvariantCulture)} MHz" : loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Manufacturer"], c.Manufacturer ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Architecture"], c.Architecture ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Socket"], c.SocketDesignation ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.L2Cache"], c.L2CacheKb.HasValue ? $"{(c.L2CacheKb.Value / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} MB" : loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.L3Cache"], c.L3CacheKb.HasValue ? $"{(c.L3CacheKb.Value / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} MB" : loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.ProcessorId"], c.ProcessorId ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Virtualization"], EnabledDisabled(c.VirtualizationFirmwareEnabled, loc)));
                    sections.Add(new DetailSection($"{loc["Nav.Cpu"]} {i + 1}", rows));
                }
                if (inv.Cpus.Count == 0)
                {
                    sections.Add(new DetailSection(loc["Detail.Inventory"],
                        [new StaticRow(loc["Common.NotAvailable"], loc["Common.NotAvailable"])]));
                }
                AddMetric(metricRows, formatters, selectors, loc["Detail.CpuUsage"],
                    v => Format.Pct(v.Snapshot.Metrics.CpuUsagePercent), m => m.CpuUsagePercent);
                AddMetric(metricRows, formatters, selectors, loc["Detail.CpuFreq"],
                    v => Format.FreqGhz(v.Snapshot.Metrics.CpuFrequencyMhz), m => m.CpuFrequencyMhz);
                sensorFilter = v => v.Snapshot.Metrics.Sensors.Where(s => HardwareClassifier.MatchesCpuHw(s.HardwareName)).OrderBy(s => s.SensorName).ToList();
                break;
            }
            case AppPage.Memory:
            {
                for (var i = 0; i < inv.MemoryModules.Count; i++)
                {
                    var mm = inv.MemoryModules[i];
                    var title = string.IsNullOrWhiteSpace(mm.DeviceLocator)
                        ? $"{loc["Nav.Memory"]} {i + 1}"
                        : $"{loc["Nav.Memory"]} {i + 1} · {mm.DeviceLocator}";
                    var rows = new List<IDetailRow>();
                    rows.Add(new StaticRow(loc["Detail.Model"], $"{mm.Manufacturer ?? loc["Common.Unknown"]} {mm.PartNumber ?? ""}".Trim()));
                    rows.Add(new StaticRow(loc["Detail.Capacity"], $"{Format.Bytes(ParseBytes(mm.CapacityBytes))}  {mm.Speed ?? loc["Common.NotAvailable"]} MHz"));
                    rows.Add(new StaticRow(loc["Detail.Type"], mm.MemoryType ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.ConfigClock"], mm.ConfiguredClockMhz ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Voltage"], mm.ConfiguredVoltageMv ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Serial"], mm.SerialNumber ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Slot"], mm.DeviceLocator ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.FormFactor"], mm.FormFactor ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Ecc"], YesNo(mm.Ecc, loc)));
                    sections.Add(new DetailSection(title, rows));
                }
                if (inv.MemoryModules.Count == 0)
                {
                    sections.Add(new DetailSection(loc["Detail.Inventory"],
                        [new StaticRow(loc["Common.NotAvailable"], loc["Common.NotAvailable"])]));
                }
                if (inv.MemoryTopology is { } mt)
                {
                    var topoRows = new List<IDetailRow>
                    {
                        new StaticRow(loc["Detail.TotalSlots"], mt.TotalSlots?.ToString() ?? loc["Common.NotAvailable"]),
                        new StaticRow(loc["Detail.UsedSlots"], mt.UsedSlots?.ToString() ?? loc["Common.NotAvailable"]),
                        new StaticRow(loc["Detail.MaxCapacity"], Format.Bytes(mt.MaxCapacityBytes)),
                        new StaticRow(loc["Detail.ErrorCorrection"], mt.ErrorCorrection ?? loc["Common.NotAvailable"]),
                    };
                    sections.Add(new DetailSection(loc["Detail.MemoryTopology"], topoRows));
                }
                AddMetric(metricRows, formatters, selectors, loc["Detail.MemUsage"],
                    v => Format.Pct(v.Snapshot.Metrics.MemoryUsagePercent), m => m.MemoryUsagePercent);
                AddMetric(metricRows, formatters, selectors, loc["Detail.MemUsed"],
                    v => $"{Format.Bytes(v.Snapshot.Metrics.MemoryUsedBytes)} / {Format.Bytes(v.Snapshot.Metrics.MemoryTotalBytes)}", null);
                break;
            }
            case AppPage.Disk:
            {
                for (var i = 0; i < inv.Disks.Count; i++)
                {
                    var d = inv.Disks[i];
                    var title = string.IsNullOrWhiteSpace(d.Model)
                        ? $"{loc["Nav.Disk"]} {i + 1}"
                        : $"{loc["Nav.Disk"]} {i + 1} · {d.Model}";
                    var rows = new List<IDetailRow>();
                    rows.Add(new StaticRow(loc["Detail.Model"], d.Model ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Capacity"], Format.Bytes(d.CapacityBytes)));
                    rows.Add(new StaticRow(loc["Detail.Serial"], d.SerialNumber ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.MediaType"], d.MediaType ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.BusType"], d.BusType ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Firmware"], d.FirmwareVersion ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Health"], HealthText(d.Health, loc)));
                    sections.Add(new DetailSection(title, rows));
                }
                if (inv.Disks.Count == 0)
                {
                    sections.Add(new DetailSection(loc["Detail.Inventory"],
                        [new StaticRow(loc["Common.NotAvailable"], loc["Common.NotAvailable"])]));
                }
                if (inv.LogicalDisks.Count > 0)
                {
                    var logicalRows = inv.LogicalDisks
                        .OrderBy(d => d.DeviceId)
                        .Select(d => (IDetailRow)new StaticRow(
                            $"{d.DeviceId ?? ""}  {d.VolumeName ?? ""}".Trim(),
                            $"{DriveTypeText(d.DriveType, loc)}   {loc["Detail.FileSystem"]} {d.FileSystem ?? "—"}   {loc["Detail.TotalSpace"]} {Format.Bytes(d.TotalBytes)}   {loc["Detail.FreeSpace"]} {Format.Bytes(d.FreeBytes)}"))
                        .ToList();
                    sections.Add(new DetailSection(loc["Detail.LogicalDisks"], logicalRows));
                }
                AddMetric(metricRows, formatters, selectors, loc["Detail.DiskRead"],
                    v => Format.Bps(v.Snapshot.Metrics.DiskReadBytesPerSec), m => m.DiskReadBytesPerSec);
                AddMetric(metricRows, formatters, selectors, loc["Detail.DiskWrite"],
                    v => Format.Bps(v.Snapshot.Metrics.DiskWriteBytesPerSec), m => m.DiskWriteBytesPerSec);
                // 磁盘传感器：按磁盘型号匹配 LHM 存储传感器 + SMART/剩余寿命/磨损
                sensorFilter = v =>
                {
                    var models = v.Snapshot.Inventory.Disks.Select(d => d.Model).Where(m => !string.IsNullOrEmpty(m)).ToList();
                    return v.Snapshot.Metrics.Sensors.Where(s =>
                        models.Any(m => !string.IsNullOrEmpty(m) && s.HardwareName.Contains(m, StringComparison.OrdinalIgnoreCase))
                        || s.SensorName.Contains("SMART", StringComparison.OrdinalIgnoreCase)
                        || s.SensorName.Contains("Remaining Life", StringComparison.OrdinalIgnoreCase)
                        || s.SensorName.Contains("Wear", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s.HardwareName).ThenBy(s => s.SensorName)
                    .ToList();
                };
                smartFilter = v => v.Snapshot.Metrics.SmartAttributes;
                break;
            }
            case AppPage.Gpu:
            {
                for (var i = 0; i < inv.Gpus.Count; i++)
                {
                    var g = inv.Gpus[i];
                    var rows = new List<IDetailRow>();
                    rows.Add(new StaticRow(loc["Detail.Model"], g.Name ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Driver"], g.DriverVersion ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.DriverDate"], g.DriverDate ?? loc["Common.NotAvailable"]));
                    if (g.CurrentHorizontalResolution.HasValue && g.CurrentVerticalResolution.HasValue)
                    {
                        rows.Add(new StaticRow(loc["Detail.Resolution"],
                            $"{g.CurrentHorizontalResolution} × {g.CurrentVerticalResolution}"));
                    }
                    if (g.CurrentRefreshRate.HasValue)
                    {
                        rows.Add(new StaticRow(loc["Detail.RefreshRate"], g.CurrentRefreshRate.Value + " Hz"));
                    }
                    rows.Add(new StaticRow(loc["Detail.VideoMode"], g.VideoModeDescription ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.VideoProcessor"], g.VideoProcessor ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.VideoArchitecture"], g.VideoArchitecture ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.AdapterCompatibility"], g.AdapterCompatibility ?? loc["Common.NotAvailable"]));
                    sections.Add(new DetailSection($"{loc["Nav.Gpu"]} {i + 1}", rows));
                }
                var vramTotalMb = GpuSensorSum(vm, "GPU Memory Total");
                sections.Add(new DetailSection(loc["Detail.VramTotal"],
                    [new StaticRow(loc["Detail.VramTotal"], vramTotalMb.HasValue ? Format.Bytes(vramTotalMb.Value * 1024 * 1024) : loc["Common.NotAvailable"])]));
                if (inv.Gpus.Count == 0)
                {
                    sections.Add(new DetailSection(loc["Detail.Inventory"],
                        [new StaticRow(loc["Common.NotAvailable"], loc["Common.NotAvailable"])]));
                }
                AddMetric(metricRows, formatters, selectors, loc["Detail.GpuUsage"],
                    v => Format.Pct(v.Snapshot.Metrics.GpuUsagePercent), m => m.GpuUsagePercent);
                AddMetric(metricRows, formatters, selectors, loc["Detail.GpuFreq"],
                    v => Format.FreqMhz(GpuClockMhz(v)), m => GpuClockMhzFrom(m));
                AddMetric(metricRows, formatters, selectors, loc["Detail.VramUsed"],
                    v => Format.Bytes(GpuSensorSum(v, "GPU Memory Used") * 1024 * 1024),
                    m => GpuSensorSumFrom(m, "GPU Memory Used") * 1024 * 1024);
                AddMetric(metricRows, formatters, selectors, loc["Detail.VramFree"],
                    v => Format.Bytes(GpuSensorSum(v, "GPU Memory Free") * 1024 * 1024),
                    m => GpuSensorSumFrom(m, "GPU Memory Free") * 1024 * 1024);
                AddMetric(metricRows, formatters, selectors, loc["Detail.VramUsage"],
                    VramUsageText, null);
                // F6：GPU 引擎拆分 + GPU 相关传感器
                sensorFilter = v =>
                {
                    var list = v.Snapshot.Metrics.GpuEngines
                        .Select(e => new SensorReading("GPU Engine", e.EngineType, e.Percent, "%"))
                        .ToList();
                    list.AddRange(v.Snapshot.Metrics.Sensors
                        .Where(s => HardwareClassifier.MatchesGpuHw(s.HardwareName))
                        .OrderBy(s => s.HardwareName).ThenBy(s => s.SensorName));
                    return list.OrderBy(s => s.HardwareName).ThenBy(s => s.SensorName).ToList();
                };
                break;
            }
            case AppPage.Motherboard:
            {
                var rows = new List<IDetailRow>
                {
                    new StaticRow(loc["Detail.AppVersion"], vm.AppVersion),
                    new StaticRow(loc["Detail.Computer"], inv.ComputerName ?? loc["Common.NotAvailable"]),
                    new StaticRow(loc["Detail.Os"], $"{inv.OsCaption ?? ""} {inv.OsVersion ?? ""}".Trim()),
                    new StaticRow(loc["Detail.OsArch"], inv.OsArchitecture ?? loc["Common.NotAvailable"]),
                    new StaticRow(loc["Detail.FirmwareType"], inv.FirmwareType ?? loc["Common.NotAvailable"]),
                    new StaticRow(loc["Detail.InstallDate"], inv.OsInstallDate ?? loc["Common.NotAvailable"]),
                    new StaticRow(loc["Detail.LastBoot"], inv.LastBootTime ?? loc["Common.NotAvailable"]),
                };
                if (inv.SystemDetails is { } sd)
                {
                    rows.Add(new StaticRow(loc["Detail.Domain"], sd.Domain ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.PartOfDomain"], YesNo(sd.PartOfDomain, loc)));
                    rows.Add(new StaticRow(loc["Detail.TimeZone"], sd.TimeZone ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.SecureBoot"], EnabledDisabled(sd.SecureBoot, loc)));
                    rows.Add(new StaticRow(loc["Detail.Tpm"], sd.TpmVersion ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Hypervisor"], YesNo(sd.HypervisorPresent, loc)));
                    rows.Add(new StaticRow(loc["Detail.SystemType"], sd.SystemType ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Serial"], sd.SerialNumber ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Uuid"], sd.Uuid ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.ProductName"], sd.ProductName ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.ProductVersion"], sd.ProductVersion ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Vbs"], VbsStatusText(sd.VirtualizationBasedSecurityStatus, loc)));
                    rows.Add(new StaticRow(loc["Detail.MemoryIntegrity"], EnabledDisabled(sd.MemoryIntegrityEnabled, loc)));
                    rows.Add(new StaticRow(loc["Detail.CodeIntegrity"], CodeIntegrityText(sd.CodeIntegrityStatus, loc)));
                }
                if (inv.Motherboard is { } mb)
                {
                    rows.Add(new StaticRow(loc["Detail.Manufacturer"], mb.Manufacturer ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Product"], mb.Product ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Bios"], mb.BiosVersion ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.BiosDate"], mb.BiosDate ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.SystemManufacturer"], mb.SystemManufacturer ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.SystemModel"], mb.SystemModel ?? loc["Common.NotAvailable"]));
                }
                sections.Add(new DetailSection(loc["Detail.Inventory"], rows));
                AddMetric(metricRows, formatters, selectors, loc["Detail.Uptime"],
                    v => Format.Uptime(v.Snapshot.Metrics.SystemUptimeSeconds, v.Loc.CurrentLanguage), null);
                break;
            }
            case AppPage.Bios:
            {
                var rows = new List<IDetailRow>();
                if (inv.Bios is { } b)
                {
                    rows.Add(new StaticRow(loc["Detail.Manufacturer"], b.Manufacturer ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Model"], b.Name ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.BiosVersion"], b.Version ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.SmbiosVersion"], b.SmbiosVersion ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.BiosDate"], b.ReleaseDate ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Serial"], b.SerialNumber ?? loc["Common.NotAvailable"]));
                    if (b.SystemBiosMajorVersion.HasValue || b.SystemBiosMinorVersion.HasValue)
                    {
                        rows.Add(new StaticRow(loc["Detail.SystemBiosVersion"],
                            $"{b.SystemBiosMajorVersion?.ToString() ?? "?"}.{b.SystemBiosMinorVersion?.ToString() ?? "?"}"));
                    }
                    if (b.EmbeddedControllerMajorVersion.HasValue || b.EmbeddedControllerMinorVersion.HasValue)
                    {
                        rows.Add(new StaticRow(loc["Detail.EcVersion"],
                            $"{b.EmbeddedControllerMajorVersion?.ToString() ?? "?"}.{b.EmbeddedControllerMinorVersion?.ToString() ?? "?"}"));
                    }
                    rows.Add(new StaticRow(loc["Detail.Description"], b.Description ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.BuildNumber"], b.BuildNumber ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.IdentificationCode"], b.IdentificationCode ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.LanguageEdition"], b.LanguageEdition ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.PrimaryBios"],
                        b.PrimaryBios == true ? loc["Detail.Yes"] : b.PrimaryBios == false ? loc["Detail.No"] : loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Status"], b.Status ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.BiosReadOnly"], ""));
                }
                else
                {
                    rows.Add(new StaticRow(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
                }
                sections.Add(new DetailSection(loc["Detail.Bios"], rows));
                break;
            }
            case AppPage.Network:
            {
                // 物理接口：所有物理网卡 + 详细信息（MAC / 类型 / 速率 / 连接状态 / 制造商 / PNP ID）
                var physical = inv.NetworkAdapters
                    .Where(n => n.IsPhysical == true
                                || (n.IsPhysical is null && !HardwareClassifier.IsVirtualNetworkAdapter(n.Name, n.AdapterType))
                                || (n.NetConnectionStatus.HasValue && !HardwareClassifier.IsVirtualNetworkAdapter(n.Name, n.AdapterType)))
                    .ToList();
                var ifRows = new List<IDetailRow>();
                foreach (var n in physical)
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(n.MacAddress)) parts.Add($"{loc["Detail.Mac"]} {n.MacAddress}");
                    if (!string.IsNullOrWhiteSpace(n.AdapterType)) parts.Add($"{loc["Detail.NetType"]} {n.AdapterType}");
                    if (n.SpeedBps is long sp && sp > 0) parts.Add($"{loc["Detail.NetSpeed"]} {Format.LinkSpeed(sp)}");
                    parts.Add($"{loc["Detail.ConnectionStatus"]} {NetStatusText(n.NetConnectionStatus, loc)}");
                    if (!string.IsNullOrWhiteSpace(n.Manufacturer)) parts.Add($"{loc["Detail.Manufacturer"]} {n.Manufacturer}");
                    if (!string.IsNullOrWhiteSpace(n.PnpDeviceId)) parts.Add($"{loc["Detail.PnpDeviceId"]} {n.PnpDeviceId}");
                    if (!string.IsNullOrWhiteSpace(n.DriverVersion)) parts.Add($"{loc["Detail.Driver"]} {n.DriverVersion}");
                    if (!string.IsNullOrWhiteSpace(n.DriverDate)) parts.Add($"{loc["Detail.DriverDate"]} {n.DriverDate}");
                    var cfg = n.Index.HasValue
                        ? inv.NetworkConfigurations.FirstOrDefault(c => c.Index == n.Index)
                        : null;
                    if (cfg is not null && n.NetConnectionStatus == 2)
                    {
                        var (ipv4, ipv6) = SplitIpVersions(cfg.IpAddresses);
                        if (ipv4.Count > 0) parts.Add($"{loc["Detail.Ipv4"]}: {Join(ipv4)}");
                        if (ipv6.Count > 0) parts.Add($"{loc["Detail.Ipv6"]}: {Join(ipv6)}");
                        if (cfg.Gateways.Count > 0) parts.Add($"{loc["Detail.Gateway"]}: {Join(cfg.Gateways)}");
                        if (cfg.DnsServers.Count > 0) parts.Add($"{loc["Detail.Dns"]}: {Join(cfg.DnsServers)}");
                    }
                    ifRows.Add(new StaticRow(n.Name ?? loc["Common.NotAvailable"], string.Join("   ", parts)));
                }
                if (ifRows.Count == 0) ifRows.Add(new StaticRow(loc["Common.NotAvailable"], loc["Common.NotAvailable"]));
                sections.Add(new DetailSection(loc["Detail.Interfaces"], ifRows));

                if (inv.WifiInterfaces.Count > 0)
                {
                    var wifiRows = inv.WifiInterfaces
                        .Select(w => (IDetailRow)new StaticRow(
                            w.Ssid ?? w.Name ?? loc["Common.NotAvailable"],
                            WifiDetailText(w, loc)))
                        .ToList();
                    sections.Add(new DetailSection(loc["Detail.Wifi"], wifiRows));
                }

                AddMetric(metricRows, formatters, selectors, loc["Detail.NetDown"],
                    v => Format.Bps(v.Snapshot.Metrics.NetworkDownloadBps), m => m.NetworkDownloadBps);
                AddMetric(metricRows, formatters, selectors, loc["Detail.NetUp"],
                    v => Format.Bps(v.Snapshot.Metrics.NetworkUploadBps), m => m.NetworkUploadBps);
                break;
            }
            case AppPage.Battery:
            {
                var rows = new List<IDetailRow>();
                if (inv.Battery is { } b)
                {
                    rows.Add(new StaticRow(loc["Detail.Model"], b.DeviceName ?? loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.DesignCapacity"], b.DesignedCapacityWh.HasValue ? $"{b.DesignedCapacityWh.Value.ToString("0.0", CultureInfo.InvariantCulture)} Wh" : loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.FullChargeCapacity"], b.FullChargeCapacityWh.HasValue ? $"{b.FullChargeCapacityWh.Value.ToString("0.0", CultureInfo.InvariantCulture)} Wh" : loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.CycleCount"], b.CycleCount.HasValue ? b.CycleCount.Value.ToString() : loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.Chemistry"], ChemistryText(b.Chemistry, loc)));
                    rows.Add(new StaticRow(loc["Detail.DesignVoltage"], b.DesignVoltageV.HasValue ? $"{b.DesignVoltageV.Value.ToString("0.00", CultureInfo.InvariantCulture)} V" : loc["Common.NotAvailable"]));
                    rows.Add(new StaticRow(loc["Detail.CurrentVoltage"], b.CurrentVoltageV.HasValue ? $"{b.CurrentVoltageV.Value.ToString("0.00", CultureInfo.InvariantCulture)} V" : loc["Common.NotAvailable"]));
                    if (b.FullChargeCapacityWh is double full && b.DesignedCapacityWh is double design && design > 0)
                    {
                        var health = Math.Min(100, full / design * 100);
                        rows.Add(new StaticRow(loc["Detail.BatteryLoss"], $"{Math.Max(0, 100 - health).ToString("0.0", CultureInfo.InvariantCulture)}%"));
                        rows.Add(new StaticRow(loc["Detail.Health"], $"{health.ToString("0.0", CultureInfo.InvariantCulture)}%"));
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
                sensorFilter = v => v.Snapshot.Metrics.Sensors
                    .Where(s => !hideNetworkNoise || !IsNetworkFilterNoise(s.HardwareName))
                    .OrderBy(s => s.HardwareName).ThenBy(s => s.SensorName).ToList();
                break;
        }

        if (sensorFilter is not null)
        {
            foreach (var s in sensorFilter(vm))
            {
                sensorRows.Add(new LiveRow(FormatSensorLabel(s), FormatSensorValue(s)));
            }
            var sensorList = sensorRows.Cast<IDetailRow>().ToList();
            // CPU 页：若无温度读数，给出原因提示（需管理员/硬件支持）
            if (category == AppPage.Cpu && !sensorRows.Any(r => r.Value.Contains("°C", StringComparison.Ordinal)))
            {
                sensorList.Insert(0, new StaticRow(loc["Detail.NoCpuTemp"], loc["Detail.AdminHint"]));
            }
            if (sensorList.Count == 0) sensorList.Add(new StaticRow(loc["Detail.Sensors"], loc["Detail.NoSensors"]));
            sections.Add(new DetailSection(loc["Detail.Sensors"], sensorList));
        }

        if (smartFilter is not null)
        {
            foreach (var a in smartFilter(vm))
            {
                smartRows.Add(new LiveRow(FormatSmartLabel(a), FormatSmartValue(a)));
            }
            var smartList = smartRows.Cast<IDetailRow>().ToList();
            if (smartList.Count == 0) smartList.Add(new StaticRow(loc["Detail.Smart"], loc["Detail.NoSmart"]));
            sections.Add(new DetailSection(loc["Detail.Smart"], smartList));
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
            BuiltCapturedAt = vm.Snapshot.CapturedAt,
        };
    }


    /// <summary>传感器页「隐藏网络过滤器噪音」开关：过滤 NDIS 过滤器栈的重复计数实例。</summary>
    private void NoiseFilter_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        _hideNetworkNoise = NoiseFilterBox.IsChecked == true;
        AppSettings.SetHideNetworkNoise(_hideNetworkNoise);
        AppSettings.Save();
        InvalidateModel(); // 过滤条件变化 → 重建传感器列表
        if (_lastVm is not null) Update(_lastVm);
    }

    private static bool IsNetworkFilterNoise(string? hardwareName)
    {
        var n = hardwareName ?? "";
        return n.Contains("NDIS", StringComparison.OrdinalIgnoreCase)
               || n.Contains("LightWeight Filter", StringComparison.OrdinalIgnoreCase)
               || n.Contains("WFP", StringComparison.OrdinalIgnoreCase)
               || n.Contains("QoS Packet Scheduler", StringComparison.OrdinalIgnoreCase)
               || n.Contains("Leigod", StringComparison.OrdinalIgnoreCase)
               || n.Contains("Npcap", StringComparison.OrdinalIgnoreCase)
               || n.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase)
               || n.Contains("Native WiFi Filter", StringComparison.OrdinalIgnoreCase)
               || n.Contains("Virtual WiFi Filter", StringComparison.OrdinalIgnoreCase)
               || n.Contains("WAN Miniport", StringComparison.OrdinalIgnoreCase)
               || n.Contains("Kernel Debug", StringComparison.OrdinalIgnoreCase);
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
        if (a.CurrentValue.HasValue) parts.Add($"V {a.CurrentValue.Value.ToString("0", CultureInfo.InvariantCulture)}");
        if (a.Worst.HasValue) parts.Add($"W {a.Worst.Value}");
        if (a.Threshold > 0) parts.Add($"T {a.Threshold}");
        if (!string.IsNullOrEmpty(a.RawValue)) parts.Add($"Raw {a.RawValue}");
        return string.Join("  ", parts);
    }

    private static string Join(IReadOnlyList<string> items) => items.Count > 0 ? string.Join(", ", items) : "—";

    /// <summary>把 Win32_NetworkAdapter.NetConnectionStatus 转成本地化文案。</summary>
    private static string NetStatusText(int? status, LocalizationManager loc)
    {
        if (status is not int s) return loc["Detail.NetStatus.0"];
        var key = $"Detail.NetStatus.{s}";
        var text = loc[key];
        return text == key ? loc["Detail.NetStatus.Unknown"] : text;
    }

    private static double? GpuSensorSum(MainViewModel vm, string name)
        => GpuSensorSumFrom(vm.Snapshot.Metrics, name);

    private static double? GpuSensorSumFrom(LiveMetrics m, string name)
    {
        var values = m.Sensors
            .Where(s => s.SensorName.Equals(name, StringComparison.OrdinalIgnoreCase) && s.Value.HasValue)
            .Select(s => s.Value!.Value)
            .Where(v => double.IsFinite(v))
            .ToList();
        return values.Count > 0 ? values.Sum() : null;
    }

    private static double? GpuClockMhz(MainViewModel vm)
        => vm.Snapshot.Metrics.Sensors.FirstOrDefault(s =>
            s.SensorName.Equals("GPU Core", StringComparison.OrdinalIgnoreCase) && s.Unit == "MHz")?.Value;

    private static double? GpuClockMhzFrom(LiveMetrics m)
        => m.Sensors.FirstOrDefault(s =>
            s.SensorName.Equals("GPU Core", StringComparison.OrdinalIgnoreCase) && s.Unit == "MHz")?.Value;

    private static string VramUsageText(MainViewModel vm)
    {
        var used = GpuSensorSum(vm, "GPU Memory Used");
        var total = GpuSensorSum(vm, "GPU Memory Total");
        return used.HasValue && total is > 0
            ? $"{(Math.Min(100, used.Value / total.Value * 100)).ToString("0.0", CultureInfo.InvariantCulture)}%"
            : "—";
    }

    private static string ChemistryText(string? code, LocalizationManager loc)
    {
        if (string.IsNullOrEmpty(code)) return loc["Common.NotAvailable"];
        var key = "Detail.Chem." + code.Trim();
        var name = loc[key];
        return name == key ? code : $"{code} ({name})";
    }

    private static string HealthText(StorageHealth? h, LocalizationManager loc) => h?.Status switch
    {
        HealthStatus.Good => loc["Common.Good"],
        HealthStatus.Warning => loc["Common.Warning"],
        HealthStatus.Critical => loc["Common.Critical"],
        _ => loc["Common.NotAvailable"],
    };

    private static string YesNo(bool? b, LocalizationManager loc) =>
        b == true ? loc["Detail.Yes"] : b == false ? loc["Detail.No"] : loc["Common.NotAvailable"];

    private static string EnabledDisabled(bool? b, LocalizationManager loc) =>
        b == true ? loc["Detail.Enabled"] : b == false ? loc["Detail.Disabled"] : loc["Common.NotAvailable"];

    private static string VbsStatusText(int? status, LocalizationManager loc) => status switch
    {
        0 => loc["Detail.Vbs.0"],
        1 => loc["Detail.Vbs.1"],
        2 => loc["Detail.Vbs.2"],
        _ => loc["Common.NotAvailable"],
    };

    private static string CodeIntegrityText(int? status, LocalizationManager loc) => status switch
    {
        0 => loc["Detail.CodeIntegrity.0"],
        1 => loc["Detail.CodeIntegrity.1"],
        2 => loc["Detail.CodeIntegrity.2"],
        _ => loc["Common.NotAvailable"],
    };

    private static (IReadOnlyList<string> Ipv4, IReadOnlyList<string> Ipv6) SplitIpVersions(IReadOnlyList<string> addresses)
    {
        var v4 = new List<string>();
        var v6 = new List<string>();
        foreach (var a in addresses)
        {
            if (string.IsNullOrEmpty(a)) continue;
            if (a.Contains(':')) v6.Add(a);
            else v4.Add(a);
        }
        return (v4, v6);
    }

    private static string DriveTypeText(int? t, LocalizationManager loc) => t switch
    {
        2 => loc["Detail.DriveType.Removable"],
        3 => loc["Detail.DriveType.Fixed"],
        4 => loc["Detail.DriveType.Network"],
        5 => loc["Detail.DriveType.Optical"],
        6 => loc["Detail.DriveType.Ram"],
        _ => loc["Detail.DriveType.Unknown"],
    };

    private static string WifiDetailText(WifiInterfaceInfo w, LocalizationManager loc)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(w.State)) parts.Add($"{loc["Detail.WifiState"]} {w.State}");
        if (w.SignalPercent.HasValue) parts.Add($"{loc["Detail.WifiSignal"]} {w.SignalPercent.Value.ToString("0", CultureInfo.InvariantCulture)}%");
        if (w.Channel.HasValue) parts.Add($"{loc["Detail.WifiChannel"]} {w.Channel.Value}");
        if (!string.IsNullOrWhiteSpace(w.RadioType)) parts.Add($"{loc["Detail.WifiRadioType"]} {w.RadioType}");
        if (!string.IsNullOrWhiteSpace(w.Authentication)) parts.Add($"{loc["Detail.WifiAuth"]} {w.Authentication}");
        if (w.ReceiveRateMbps.HasValue) parts.Add($"{loc["Detail.WifiRx"]} {w.ReceiveRateMbps.Value.ToString("0", CultureInfo.InvariantCulture)} Mbps");
        if (w.TransmitRateMbps.HasValue) parts.Add($"{loc["Detail.WifiTx"]} {w.TransmitRateMbps.Value.ToString("0", CultureInfo.InvariantCulture)} Mbps");
        if (!string.IsNullOrWhiteSpace(w.ConnectionMode)) parts.Add($"{loc["Detail.WifiMode"]} {w.ConnectionMode}");
        if (!string.IsNullOrWhiteSpace(w.Bssid)) parts.Add($"BSSID {w.Bssid}");
        return string.Join("   ", parts);
    }

    private static double? ParseBytes(string? s) => long.TryParse(s, out var b) && b > 0 ? b : null;
}
