using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using TecSight.App.Pages;
using TecSight.Core;

namespace TecSight.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly OverviewPage _overview = new();
    private readonly DetailPage _detail = new();
    private readonly DispatcherTimer _timer;
    private readonly PerformanceMetricsProvider _metricsProvider = new();
    private readonly LibreHardwareSensorProvider _sensorProvider = new();

    public MainWindow()
    {
        InitializeComponent();

        var collector = new HistoryCollector(
            new SnapshotCollector(
                new WmiInventoryProvider(),
                _metricsProvider,
                _sensorProvider),
            capacity: 3600);

        _vm = new MainViewModel(collector);
        DataContext = _vm;
        Title = _vm.Loc["App.Title"];

        PageHost.Content = _overview;
        _vm.CurrentPage = AppPage.Overview;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _vm.Refresh();
            UpdateCurrentPage();
        };
        _timer.Start();

        _vm.Refresh();
        UpdateCurrentPage();
    }

    private void UpdateCurrentPage()
    {
        if (_vm.CurrentPage == AppPage.Overview)
        {
            _overview.Update(_vm);
        }
        else
        {
            _detail.Update(_vm);
        }
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_vm is null) return;
        var page = _vm.CurrentPage;
        PageHost.Content = page == AppPage.Overview ? _overview : _detail;
        if (page != AppPage.Overview)
        {
            _detail.SetCategory(page);
        }
        UpdateCurrentPage();
    }

    private void LangButton_Click(object sender, RoutedEventArgs e)
    {
        _vm.Loc.CurrentLanguage = _vm.Loc.CurrentLanguage == "zh" ? "en" : "zh";
        Title = _vm.Loc["App.Title"];
        UpdateCurrentPage();
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

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _metricsProvider.Dispose();
        _sensorProvider.Dispose();
        base.OnClosed(e);
    }
}