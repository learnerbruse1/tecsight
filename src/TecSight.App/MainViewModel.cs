using System.ComponentModel;
using TecSight.App.Localization;
using TecSight.App.Models;
using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.App;

public enum AppPage { Overview, Cpu, Memory, Disk, Gpu, Motherboard, Network, Battery, Sensors, Processes }

/// <summary>导航项。</summary>
public sealed record NavEntry(AppPage Page, string Title);

/// <summary>主视图模型：持有采集器、当前快照与历史，负责页面切换。</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public LocalizationManager Loc => LocalizationManager.Instance;

    public HistoryCollector Collector { get; }
    public ISnapshotExporter Exporter { get; } = new SnapshotExporter();

    private Snapshot _snapshot = new(DateTimeOffset.MinValue, new HardwareInventory(), new LiveMetrics { Timestamp = DateTimeOffset.MinValue });
    public Snapshot Snapshot
    {
        get => _snapshot;
        private set { _snapshot = value; OnPropertyChanged(nameof(Snapshot)); OnPropertyChanged(nameof(History)); }
    }

    public IReadOnlyList<LiveMetrics> History => Collector.History;

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
        NavEntries =
        [
            new NavEntry(AppPage.Overview, Loc["Nav.Overview"]),
            new NavEntry(AppPage.Cpu, Loc["Nav.Cpu"]),
            new NavEntry(AppPage.Memory, Loc["Nav.Memory"]),
            new NavEntry(AppPage.Disk, Loc["Nav.Disk"]),
            new NavEntry(AppPage.Gpu, Loc["Nav.Gpu"]),
            new NavEntry(AppPage.Motherboard, Loc["Nav.Motherboard"]),
            new NavEntry(AppPage.Network, Loc["Nav.Network"]),
            new NavEntry(AppPage.Battery, Loc["Nav.Battery"]),
            new NavEntry(AppPage.Sensors, Loc["Nav.Sensors"]),
            new NavEntry(AppPage.Processes, Loc["Nav.Processes"]),
        ];
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}