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
    private DateTimeOffset _lastScan = DateTimeOffset.MinValue;
    private bool _scanning;
    private bool _pendingRefresh;

    public PeripheralsPage() => InitializeComponent();

    /// <summary>每帧调用（1 秒），内部按 10 秒节流（Win32_PnPEntity 枚举较慢 ~650ms），在后台线程扫描外设。</summary>
    public void Update(MainViewModel vm)
    {
        _vm = vm;
        MaybeScanAsync();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _lastScan = DateTimeOffset.MinValue;
        MaybeScanAsync();
    }

    private void MaybeScanAsync()
    {
        if (_vm is null) return;
        if (_scanning)
        {
            _pendingRefresh = true; // 扫描进行中再点刷新：标记待办，当前扫描完成后立即再扫一次
            return;
        }
        if (DateTimeOffset.UtcNow - _lastScan < TimeSpan.FromSeconds(10)) return;
        _scanning = true;
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
                _scanning = false;
                _lastScan = DateTimeOffset.UtcNow; // 成功或失败都推进节流，避免退化为每秒扫描
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
            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                _lastScan = DateTimeOffset.MinValue;
                MaybeScanAsync();
            }
        });
    }

    private void Show(MainViewModel vm, IReadOnlyList<PeripheralDevice> devices)
    {
        _lastScan = DateTimeOffset.UtcNow;
        Groups.ItemsSource = devices
            .GroupBy(d => d.Category)
            .OrderBy(g => Array.IndexOf(CategoryOrder, g.Key) is var idx && idx >= 0 ? idx : CategoryOrder.Length)
            .Select(g => new PeripheralGroup(
                $"{vm.Loc["Peripheral." + g.Key]}  ({g.Count()})",
                g.Select(d => BuildItem(d, vm.Loc)).ToList()))
            .ToList();
        CountText.Text = $"{vm.Loc["Peripheral.Count"]} {devices.Count}  ·  {vm.Loc["Peripheral.UpdatedAt"]} {DateTime.Now:HH:mm:ss}";
        EmptyText.Text = vm.Loc["Peripheral.None"];
        EmptyText.Visibility = devices.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    private static PeripheralItem BuildItem(PeripheralDevice d, LocalizationManager loc)
    {
        string Un() => loc["Peripheral.Unavailable"];

        var details = new List<PeripheralField>
        {
            new(loc["Detail.Manufacturer"], d.Manufacturer ?? Un()),
            new(loc["Detail.Type"], d.PnpClass ?? Un()),
            new(loc["Detail.Status"], d.Status ?? Un()),
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
        if (!string.IsNullOrWhiteSpace(d.Status)) summary.Add(d.Status!);
        if (!string.IsNullOrWhiteSpace(d.PnpClass)) summary.Add(d.PnpClass!);

        return new PeripheralItem(d.Name ?? "?", string.Join(" · ", summary), details);
    }
}
