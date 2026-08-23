using System.Windows;
using System.Windows.Controls;
using TecSight.App.Localization;
using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.App.Pages;

public partial class PeripheralsPage : UserControl
{
    private sealed record PeripheralGroup(string Title, IReadOnlyList<PeripheralItem> Items);
    private sealed record PeripheralItem(string Name, string Summary, IReadOnlyList<PeripheralField> Details);
    private sealed record PeripheralField(string Label, string Value);

    private static readonly string[] CategoryOrder =
        ["storage", "keyboard", "mouse", "camera", "audio", "display", "network",
         "bluetooth", "printer", "cardreader", "gamepad", "phone", "hub", "input", "usb", "other"];

    private MainViewModel? _vm;
    private IReadOnlyList<PeripheralDevice>? _lastDevices;
    private string _lastLanguage = "";
    private DateTimeOffset _lastScan = DateTimeOffset.MinValue;
    private bool _scanning;
    private bool _pendingRefresh;
    private readonly object _scanGate = new();

    public PeripheralsPage() => InitializeComponent();

    /// <summary>每帧调用（1 秒），内部按 10 秒节流（Win32_PnPEntity 枚举较慢 ~650ms），在后台线程扫描外设。</summary>
    public void Update(MainViewModel vm)
    {
        _vm = vm;
        if (_lastDevices is null)
        {
            CountText.Text = $"{vm.Loc["Peripheral.Count"]} {vm.Loc["Common.NotAvailable"]}";
        }
        MaybeScanAsync();
        if (_lastDevices is not null && !string.Equals(_lastLanguage, vm.Loc.CurrentLanguage, StringComparison.Ordinal))
        {
            Show(vm, _lastDevices);
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        lock (_scanGate)
        {
            _lastScan = DateTimeOffset.MinValue;
        }
        MaybeScanAsync();
    }

    private void MaybeScanAsync()
    {
        if (_vm is null) return;
        lock (_scanGate)
        {
            if (_scanning)
            {
                _pendingRefresh = true; // 扫描进行中再点刷新：标记待办，当前扫描完成后立即再扫一次
                return;
            }
            if (DateTimeOffset.UtcNow - _lastScan < TimeSpan.FromSeconds(AppSettings.PeripheralScanSeconds)) return;
            _scanning = true;
        }

        _ = Task.Run(() =>
        {
            IReadOnlyList<PeripheralDevice>? devices = null;
            try
            {
                devices = PeripheralProbe.Scan();
            }
            catch
            {
                // 降级
            }
            finally
            {
                lock (_scanGate)
                {
                    _scanning = false;
                    _lastScan = DateTimeOffset.UtcNow; // 成功或失败都推进节流，避免退化为每秒扫描
                }
            }
            if (devices is not null && !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                var items = devices;
                try
                {
                    Dispatcher.Invoke(() => Show(_vm!, items));
                }
                catch
                {
                    // 窗口在检查与投递之间关闭等竞态：忽略
                }
            }

            bool refreshPending;
            lock (_scanGate)
            {
                refreshPending = _pendingRefresh;
                if (refreshPending)
                {
                    _pendingRefresh = false;
                    _lastScan = DateTimeOffset.MinValue;
                }
            }
            if (refreshPending) MaybeScanAsync();
        });
    }

    private void Show(MainViewModel vm, IReadOnlyList<PeripheralDevice> devices)
    {
        _lastDevices = devices;
        _lastLanguage = vm.Loc.CurrentLanguage;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<PeripheralDevice>();
        foreach (var d in devices.Concat(PeripheralProbe.FromInventory(vm.Snapshot.Inventory)))
        {
            if (!string.IsNullOrWhiteSpace(d.PnpDeviceId))
            {
                var key = BuildExactKey(d);
                if (seen.Add(key)) merged.Add(d);
            }
            else if (merged.Any(x => SoftDeviceMatch(x, d)))
            {
                continue; // 硬件清单中的无 PNP ID 项，如果与已扫描设备同型号则去重
            }
            else
            {
                merged.Add(d);
            }
        }

        Groups.ItemsSource = merged
            .GroupBy(d => d.Category)
            .OrderBy(g => Array.IndexOf(CategoryOrder, g.Key) is var idx && idx >= 0 ? idx : CategoryOrder.Length)
            .Select(g => new PeripheralGroup(
                $"{vm.Loc["Peripheral." + g.Key]}  ({g.Count()})",
                g.Select(d => BuildItem(d, vm.Loc)).ToList()))
            .ToList();
        CountText.Text = $"{vm.Loc["Peripheral.Count"]} {merged.Count}  ·  {vm.Loc["Peripheral.UpdatedAt"]} {DateTime.Now:HH:mm:ss}";
        EmptyText.Text = vm.Loc["Peripheral.None"];
            EmptyText.Visibility = merged.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    private static string BuildExactKey(PeripheralDevice d) => d.PnpDeviceId!.Trim().ToUpperInvariant();

    /// <summary>
    /// 用于无 PNP ID 的硬件清单设备与已扫描设备去重：类别和名称必须一致；
    /// 仅当双方都有厂商信息且不一致时才认为不是同一设备，避免 PnP/WMI 厂商字段缺失造成重复。
    /// </summary>
    private static bool SoftDeviceMatch(PeripheralDevice a, PeripheralDevice b)
    {
        if (!string.Equals(a.Category, b.Category, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(a.Name?.Trim(), b.Name?.Trim(), StringComparison.OrdinalIgnoreCase)) return false;

        var ma = a.Manufacturer?.Trim();
        var mb = b.Manufacturer?.Trim();
        return string.IsNullOrEmpty(ma)
               || string.IsNullOrEmpty(mb)
               || string.Equals(ma, mb, StringComparison.OrdinalIgnoreCase);
    }

    private static PeripheralItem BuildItem(PeripheralDevice d, LocalizationManager loc)
    {
        string Un() => loc["Peripheral.Unavailable"];
        var status = d.Status;
        if (d.Category == "network" && int.TryParse(d.Status, out var netStatus))
        {
            var key = $"Detail.NetStatus.{netStatus}";
            var text = loc[key];
            status = text == key ? loc["Detail.NetStatus.Unknown"] : text;
        }

        var details = new List<PeripheralField>
        {
            new(loc["Detail.Manufacturer"], d.Manufacturer ?? Un()),
            new(loc["Detail.Type"], d.PnpClass ?? Un()),
            new(loc["Detail.Status"], status ?? Un()),
            new(loc["Peripheral.Resolution"], d.Resolution ?? Un()),
            new(loc["Peripheral.RefreshRate"], d.RefreshRate.HasValue ? d.RefreshRate.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " Hz" : Un()),
            new(loc["Detail.Serial"], d.SerialNumber ?? Un()),
            new(loc["Peripheral.Year"], d.ManufactureYear.HasValue ? d.ManufactureYear.Value.ToString() : Un()),
            new(loc["Detail.Description"],
                string.IsNullOrWhiteSpace(d.Description) || string.Equals(d.Description, d.Name, StringComparison.Ordinal)
                    ? Un()
                    : d.Description),
            new(loc["Detail.PnpDeviceId"], d.PnpDeviceId ?? Un()),
        };

        var (vid, pid) = PeripheralProbe.ParseUsbVidPid(d.PnpDeviceId ?? d.HardwareId);
        if (vid is not null || pid is not null)
        {
            details.Add(new PeripheralField(loc["Peripheral.Vid"], vid ?? Un()));
            details.Add(new PeripheralField(loc["Peripheral.Pid"], pid ?? Un()));
        }

        details.Add(new PeripheralField(loc["Peripheral.DriverProvider"], d.DriverProvider ?? Un()));
        details.Add(new PeripheralField(loc["Peripheral.DriverVersion"], d.DriverVersion ?? Un()));
        details.Add(new PeripheralField(loc["Detail.DriverDate"], d.DriverDate ?? Un()));
        details.Add(new PeripheralField(loc["Peripheral.Service"], d.Service ?? Un()));
        details.Add(new PeripheralField(loc["Peripheral.DeviceId"], d.DeviceId ?? Un()));

        var summary = new List<string>();
        if (!string.IsNullOrWhiteSpace(d.Detail)) summary.Add(d.Detail!);
        if (!string.IsNullOrWhiteSpace(d.Manufacturer)) summary.Add(d.Manufacturer!);
        if (!string.IsNullOrWhiteSpace(status)) summary.Add(status);
        if (!string.IsNullOrWhiteSpace(d.PnpClass)) summary.Add(d.PnpClass!);

        return new PeripheralItem(d.Name ?? "?", string.Join(" · ", summary), details);
    }
}
