using System.Diagnostics;
using System.Security.Principal;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
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
    private string _realTitle = "TecSight";
    private DispatcherTimer? _transientTimer;

    public MainWindow()
    {
        // 应用持久化设置（主题/语言/传感器噪音开关）
        AppSettings.Load();
        _detail.SetHideNetworkNoise(AppSettings.HideNetworkNoise);
        if (AppSettings.DarkTheme) ThemeManager.SetDark(true);
        if (!string.IsNullOrEmpty(AppSettings.Language))
        {
            Localization.LocalizationManager.Instance.CurrentLanguage = AppSettings.Language;
        }

        InitializeComponent();

        // 标题栏随主题变深/浅色（DWM）
        SourceInitialized += (_, _) => ApplyTitleBarTheme();

        // 主题按钮与已保存/已应用的主题保持一致
        ThemeButton.Content = ThemeManager.IsDark ? "☀️" : "🌙";

        // 已提权时隐藏"以管理员重启"按钮（无意义）
        AdminRestartButton.Visibility = IsElevated() ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

        ApplySavedWindowState();

        var collector = new HistoryCollector(
            new SnapshotCollector(
                new CachedInventoryProvider(new WmiInventoryProvider()),
                _metricsProvider,
                _sensorProvider),
            capacity: 3600);

        _vm = new MainViewModel(collector);
        DataContext = _vm;
        _realTitle = _vm.Loc["App.Title"];
        Title = _realTitle;

        PageHost.Content = _overview;
        _vm.CurrentPage = AppPage.Overview;
        _overview.Update(_vm); // 首帧即显示占位卡片，避免空白闪屏

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
                try
                {
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
                catch
                {
                    // 窗口在检查与投递之间关闭等竞态：忽略
                }
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
        _realTitle = _vm.Loc["App.Title"];
        Title = _realTitle;
        _detail.InvalidateModel(); // 详情页静态标签按新语言重建
        UpdateCurrentPage();
        AppSettings.Save();
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e) => ToggleTheme();

    /// <summary>切换深色/浅色主题，并同步按钮图标与持久化设置（主题按钮 / F11 共用）。</summary>
    private void ToggleTheme()
    {
        ThemeManager.Toggle();
        ThemeButton.Content = ThemeManager.IsDark ? "☀️" : "🌙";
        ApplyTitleBarTheme();
        AppSettings.Save();
    }

    /// <summary>让 Windows 标题栏跟随应用深浅主题（DWM 沉浸式深色）。</summary>
    private void ApplyTitleBarTheme()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            var dark = ThemeManager.IsDark ? 1 : 0;
            // Windows 10 2004+ / 11 使用属性 20；旧版 1809-1903 使用 19
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, sizeof(int));
            }
        }
        catch
        {
            // DWM 不可用时保持系统标题栏
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

    /// <summary>以管理员权限重启，以便读取需要内核驱动的传感器（CPU 温度/风扇等）。</summary>
    private void AdminRestart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exe = Environment.ProcessPath ?? "TecSight.App.exe";
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "--restart-as-admin",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory,
            };
            var elevated = Process.Start(psi);
            if (elevated is null)
            {
                return; // 启动失败：保持当前会话
            }
            Application.Current.Shutdown();
        }
        catch
        {
            // UAC 被取消或启动失败：保持当前会话（不关闭当前实例）
        }
    }

    private void CompatButton_Click(object sender, RoutedEventArgs e) => ExportCompat();

    private void CopySummary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_vm.Exporter.ExportTxt(_vm.Snapshot));
            ShowTransientStatus(_vm.Loc["Common.Copied"]);
        }
        catch
        {
            // 剪贴板被占用等异常时忽略
        }
    }

    /// <summary>在标题栏短暂显示状态提示后恢复真实标题（连续点击安全）。</summary>
    private void ShowTransientStatus(string message)
    {
        if (_transientTimer is not null)
        {
            _transientTimer.Stop();
        }
        Title = message;
        _transientTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _transientTimer.Tick += (_, _) =>
        {
            Title = _realTitle;
            _transientTimer!.Stop();
        };
        _transientTimer.Start();
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

    private void ExportHistoryCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            FileName = $"tecsight-history-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            Filter = "CSV 文件 (*.csv)|*.csv",
        };
        if (dlg.ShowDialog(this) != true) return;
        File.WriteAllText(dlg.FileName, _vm.Exporter.ExportHistoryCsv(_vm.History));
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

    /// <summary>快捷键：Ctrl+E 打开导出菜单；F5 手动刷新。</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ExportMenuItem.IsSubmenuOpen = true;
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            CollectAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.F11)
        {
            ToggleTheme();
            e.Handled = true;
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _metricsProvider.Dispose();
        _sensorProvider.Dispose();

        // 保存窗口位置/大小/最大化状态（最小化时用还原边界）
        if (WindowState != WindowState.Minimized)
        {
            // RestoreBounds 在最大化/最小化时为还原边界，在普通状态为当前边界
            var rb = RestoreBounds;
            if (rb.Width <= 0 || rb.Height <= 0)
            {
                rb = new Rect(Left, Top, Width, Height);
            }
            AppSettings.SaveWindow(rb.Left, rb.Top, rb.Width, rb.Height, WindowState == WindowState.Maximized);
        }
        base.OnClosed(e);
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
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