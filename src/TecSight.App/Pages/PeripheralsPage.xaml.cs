using System.Windows;
using System.Windows.Controls;
using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.App.Pages;

public partial class PeripheralsPage : UserControl
{
    private sealed record PeripheralGroup(string Title, IReadOnlyList<PeripheralRow> Rows);
    private sealed record PeripheralRow(string Name, string Detail);

    private static readonly string[] CategoryOrder =
        ["storage", "keyboard", "mouse", "camera", "audio", "display", "network",
         "bluetooth", "printer", "cardreader", "gamepad", "phone", "hub", "input", "usb", "other"];

    private MainViewModel? _vm;
    private DateTimeOffset _lastScan = DateTimeOffset.MinValue;
    private bool _scanning;

    public PeripheralsPage() => InitializeComponent();

    /// <summary>每帧调用（1 秒），内部按 5 秒节流，在后台线程扫描外设。</summary>
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
        if (_scanning || _vm is null) return;
        if (DateTimeOffset.UtcNow - _lastScan < TimeSpan.FromSeconds(5)) return;
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
                g.Select(d => new PeripheralRow(d.Name ?? "?", DeviceDetail(d))).ToList()))
            .ToList();
        CountText.Text = $"{vm.Loc["Peripheral.Count"]} {devices.Count}  ·  {vm.Loc["Peripheral.UpdatedAt"]} {DateTime.Now:HH:mm:ss}";
        EmptyText.Text = vm.Loc["Peripheral.None"];
        EmptyText.Visibility = devices.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    private static string DeviceDetail(PeripheralDevice d)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(d.Detail)) parts.Add(d.Detail!);
        if (!string.IsNullOrEmpty(d.Manufacturer)) parts.Add(d.Manufacturer!);
        if (!string.IsNullOrEmpty(d.Description) && !string.Equals(d.Description, d.Name, StringComparison.Ordinal)) parts.Add(d.Description!);
        if (!string.IsNullOrEmpty(d.Status)) parts.Add("状态 " + d.Status);
        if (!string.IsNullOrEmpty(d.PnpClass)) parts.Add("[" + d.PnpClass + "]");
        if (!string.IsNullOrEmpty(d.PnpDeviceId)) parts.Add(d.PnpDeviceId!);
        return string.Join("  ", parts);
    }
}