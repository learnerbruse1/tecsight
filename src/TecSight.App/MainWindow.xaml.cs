using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using TecSight.App.Pages;
using TecSight.App.Themes;
using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly OverviewPage _overview = new();
    private readonly DetailPage _detail = new();
    private readonly ProcessesPage _processes = new();
    private readonly PeripheralsPage _peripherals = new();
    private readonly DispatcherTimer _timer;
    private readonly PerformanceMetricsProvider _metricsProvider = new();
    private readonly LibreHardwareSensorProvider _sensorProvider = new();
    private readonly object _collectGate = new();
    private bool _collecting;

    public MainWindow()
    {
        // 应用持久化设置（主题/语言）
        AppSettings.Load();
        if (AppSettings.DarkTheme) ThemeManager.SetDark(true);
        if (!string.IsNullOrEmpty(AppSettings.Language))
        {
            Localization.LocalizationManager.Instance.CurrentLanguage = AppSettings.Language;
        }

        InitializeComponent();

        // 主题按钮与已保存/已应用的主题保持一致
        ThemeButton.Content = ThemeManager.IsDark ? "☀️" : "🌙";

        ApplySavedWindowState();

        var collector = new HistoryCollector(
            new SnapshotCollector(
                new CachedInventoryProvider(new WmiInventoryProvider()),
                _metricsProvider,
                _sensorProvider),
            capacity: 3600);

        _vm = new MainViewModel(collector);
        DataContext = _vm;
        Title = _vm.Loc["App.Title"];

        PageHost.Content = _overview;
        _vm.CurrentPage = AppPage.Overview;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => CollectAsync();
        _timer.Start();

        CollectAsync();
    }

    /// <summary>在后台线程采集，结果投递回 UI 线程，避免阻塞界面（修复滚动卡顿）。</summary>
    private void CollectAsync()
    {
        lock (_collectGate)
        {
            if (_collecting) return;
            _collecting = true;
        }

        _ = Task.Run(() =>
        {
            Snapshot? snapshot = null;
            try
            {
                snapshot = _vm.Collector.Collect();
            }
            catch
            {
                // 采集失败时静默降级，下次重试
            }
            finally
            {
                lock (_collectGate)
                {
                    _collecting = false;
                }
            }

            if (snapshot is not null && !Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            {
                var snap = snapshot;
                Dispatcher.Invoke(() =>
                {
                    _vm.SetSnapshot(snap);
                    // 最小化时跳过界面刷新（数据仍持续采集），恢复后下一秒自动更新
                    if (WindowState != WindowState.Minimized)
                    {
                        UpdateCurrentPage();
                    }
                });
            }
        });
    }

    private void UpdateCurrentPage()
    {
        switch (_vm.CurrentPage)
        {
            case AppPage.Overview:
                _overview.Update(_vm);
                break;
            case AppPage.Processes:
                _processes.Update(_vm);
                break;
            case AppPage.Peripherals:
                _peripherals.Update(_vm);
                break;
            default:
                _detail.Update(_vm);
                break;
        }
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        var page = _vm.CurrentPage;
        PageHost.Content = page switch
        {
            AppPage.Overview => _overview,
            AppPage.Processes => _processes,
            AppPage.Peripherals => _peripherals,
            _ => _detail,
        };
        if (page != AppPage.Overview && page != AppPage.Processes && page != AppPage.Peripherals)
        {
            _detail.SetCategory(page);
        }
        UpdateCurrentPage();
    }

    private void LangButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.Loc.CurrentLanguage = _vm.Loc.CurrentLanguage == "zh" ? "en" : "zh";
        Title = _vm.Loc["App.Title"];
        _detail.InvalidateModel(); // 详情页静态标签按新语言重建
        UpdateCurrentPage();
        AppSettings.Save();
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        ThemeButton.Content = ThemeManager.IsDark ? "☀️" : "🌙";
        AppSettings.Save();
    }

    /// <summary>以管理员权限重启，以便读取需要内核驱动的传感器（CPU 温度/风扇等）。</summary>
    private void AdminRestart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? "TecSight.App.exe",
                UseShellExecute = true,
                Verb = "runas",
            };
            Process.Start(psi);
            Application.Current.Shutdown();
        }
        catch
        {
            // 用户取消 UAC 或启动失败：保持当前会话
        }
    }

    private void CompatButton_Click(object sender, RoutedEventArgs e) => ExportCompat();

    private void CopySummary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_vm.Exporter.ExportTxt(_vm.Snapshot));
        }
        catch
        {
            // 剪贴板被占用等异常时忽略
        }
    }

    private void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            FileName = $"tecsight-{DateTime.Now:yyyyMMdd-HHmmss}.html",
            Filter = "HTML 文件 (*.html)|*.html",
        };
        if (dlg.ShowDialog(this) != true) return;
        File.WriteAllText(dlg.FileName, _vm.Exporter.ExportHtml(_vm.Snapshot));
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e) => Export("json");

    private void ExportTxt_Click(object sender, RoutedEventArgs e) => Export("txt");

    private void Export(string ext)
    {
        var dlg = new SaveFileDialog
        {
            FileName = $"tecsight-{DateTime.Now:yyyyMMdd-HHmmss}.{ext}",
            Filter = ext == "json" ? "JSON 文件 (*.json)|*.json" : "文本文件 (*.txt)|*.txt",
        };
        if (dlg.ShowDialog(this) != true) return;
        var content = ext == "json"
            ? _vm.Exporter.ExportJson(_vm.Snapshot)
            : _vm.Exporter.ExportTxt(_vm.Snapshot);
        File.WriteAllText(dlg.FileName, content);
    }

    private void ExportCompat()
    {
        var dlg = new SaveFileDialog
        {
            FileName = $"tecsight-compat-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            Filter = "文本文件 (*.txt)|*.txt",
        };
        if (dlg.ShowDialog(this) != true) return;
        File.WriteAllText(dlg.FileName, CompatibilityReporter.Build(_vm.Snapshot));
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _metricsProvider.Dispose();
        _sensorProvider.Dispose();

        // 保存窗口位置/大小/最大化状态（最小化时用还原边界）
        if (WindowState != WindowState.Minimized)
        {
            var rb = WindowState == WindowState.Normal ? RestoreBounds : new Rect(Left, Top, Width, Height);
            AppSettings.SaveWindow(rb.Left, rb.Top, rb.Width, rb.Height, WindowState == WindowState.Maximized);
        }
        base.OnClosed(e);
    }

    /// <summary>恢复上次窗口位置/大小（防止还原到屏幕外）。</summary>
    private void ApplySavedWindowState()
    {
        if (double.IsNaN(AppSettings.WindowLeft)) return;
        var w = AppSettings.WindowWidth;
        var h = AppSettings.WindowHeight;
        var x = AppSettings.WindowLeft;
        var y = AppSettings.WindowTop;
        if (w < 200 || h < 150) return;
        var sl = SystemParameters.VirtualScreenLeft;
        var st = SystemParameters.VirtualScreenTop;
        var sw = SystemParameters.VirtualScreenWidth;
        var sh = SystemParameters.VirtualScreenHeight;
        if (x + 40 > sl + sw || y + 40 > st + sh || x + w < sl + 40 || y + h < st + 40) return;
        Left = x;
        Top = y;
        Width = w;
        Height = h;
        if (AppSettings.WindowMaximized) WindowState = WindowState.Maximized;
    }
}