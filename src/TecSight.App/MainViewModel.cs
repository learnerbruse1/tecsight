using System.ComponentModel;
using System.Reflection;
using TecSight.App.Localization;
using TecSight.App.Models;
using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.App;

public enum AppPage { Overview, Cpu, Memory, Disk, Gpu, Motherboard, Network, Battery, Sensors, Processes, Peripherals, Bios }

/// <summary>导航项。语言切换时原地更新 Title（不重建列表），避免选中状态丢失与重入。ToString 便于无障碍朗读。</summary>
public sealed class NavEntry : INotifyPropertyChanged
{
    public AppPage Page { get; }

    private string _title;

    public NavEntry(AppPage page, string title)
    {
        Page = page;
        _title = title;
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value) return;
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    public override string ToString() => Title;

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>主视图模型：持有采集器、当前快照与历史，负责页面切换。</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public LocalizationManager Loc => LocalizationManager.Instance;

    public HistoryCollector Collector { get; }
    public ISnapshotExporter Exporter { get; } = new SnapshotExporter();

    private readonly Dictionary<AppPage, NavEntry> _navByPage = [];

    /// <summary>软件版本（如 2.0.0）。</summary>
    public string AppVersion { get; } = (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)) ?? "?";

    private Snapshot _snapshot = new(DateTimeOffset.MinValue, new HardwareInventory(), new LiveMetrics { Timestamp = DateTimeOffset.MinValue });
    private IReadOnlyList<LiveMetrics> _history = Array.Empty<LiveMetrics>();
    public Snapshot Snapshot
    {
        get => _snapshot;
        private set
        {
            _snapshot = value;
            _history = Collector.History;
            OnPropertyChanged(nameof(Snapshot));
            OnPropertyChanged(nameof(History));
        }
    }

    /// <summary>随每次快照更新的历史引用：避免详情页每秒为每个指标重复读取/加锁。</summary>
    public IReadOnlyList<LiveMetrics> History => _history;

    private AppPage _currentPage = AppPage.Overview;
    public AppPage CurrentPage
    {
        get => _currentPage;
        set { if (_currentPage != value) { _currentPage = value; OnPropertyChanged(nameof(CurrentPage)); } }
    }

    private IReadOnlyList<NavEntry> _navEntries = [];
    public IReadOnlyList<NavEntry> NavEntries
    {
        get => _navEntries;
        private set { _navEntries = value; OnPropertyChanged(nameof(NavEntries)); }
    }

    public MainViewModel(HistoryCollector collector)
    {
        Collector = collector;
        Loc.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "CurrentLanguage") RebuildNav();
        };
        RebuildNav();
    }

    /// <summary>由后台采集线程投递新快照到 UI 线程后调用。</summary>
    public void SetSnapshot(Snapshot snapshot) => Snapshot = snapshot;

    private void RebuildNav()
    {
        if (_navByPage.Count == 0)
        {
            var entries = new List<NavEntry>
            {
                new(AppPage.Overview, Loc["Nav.Overview"]),
                new(AppPage.Cpu, Loc["Nav.Cpu"]),
                new(AppPage.Memory, Loc["Nav.Memory"]),
                new(AppPage.Disk, Loc["Nav.Disk"]),
                new(AppPage.Gpu, Loc["Nav.Gpu"]),
                new(AppPage.Motherboard, Loc["Nav.Motherboard"]),
                new(AppPage.Bios, Loc["Nav.Bios"]),
                new(AppPage.Network, Loc["Nav.Network"]),
                new(AppPage.Battery, Loc["Nav.Battery"]),
                new(AppPage.Sensors, Loc["Nav.Sensors"]),
                new(AppPage.Processes, Loc["Nav.Processes"]),
                new(AppPage.Peripherals, Loc["Nav.Peripherals"]),
            };
            foreach (var entry in entries)
            {
                _navByPage[entry.Page] = entry;
            }
            NavEntries = entries;
            return;
        }

        // 语言切换：原地更新标题，不替换 ItemsSource（替换会导致 ListBox 选中状态丢失）。
        foreach (var (page, entry) in _navByPage)
        {
            entry.Title = Loc[TitleKey(page)];
        }
    }

    private static string TitleKey(AppPage page) => page switch
    {
        AppPage.Overview => "Nav.Overview",
        AppPage.Cpu => "Nav.Cpu",
        AppPage.Memory => "Nav.Memory",
        AppPage.Disk => "Nav.Disk",
        AppPage.Gpu => "Nav.Gpu",
        AppPage.Motherboard => "Nav.Motherboard",
        AppPage.Bios => "Nav.Bios",
        AppPage.Network => "Nav.Network",
        AppPage.Battery => "Nav.Battery",
        AppPage.Sensors => "Nav.Sensors",
        AppPage.Processes => "Nav.Processes",
        AppPage.Peripherals => "Nav.Peripherals",
        _ => "Nav.Overview",
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
