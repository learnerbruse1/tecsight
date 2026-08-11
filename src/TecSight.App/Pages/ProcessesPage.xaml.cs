using System.Windows.Controls;
using TecSight.Core.Models;

namespace TecSight.App.Pages;

public partial class ProcessesPage : UserControl
{
    private sealed record ProcRow(string Name, string Cpu, string Mem);

    public ProcessesPage() => InitializeComponent();

    public void Update(MainViewModel vm)
    {
        ProcList.ItemsSource = vm.Snapshot.Metrics.Processes
            .Select(p => new ProcRow(p.ProcessId is int pid ? $"{p.Name} ({pid})" : p.Name, p.CpuPercent.HasValue ? $"{p.CpuPercent.Value:0.0}%" : "…", Format.Bytes(p.WorkingSetBytes)))
            .ToList();
        TotalText.Text = $"{vm.Loc["Process.Total"]} {vm.Snapshot.Metrics.TotalProcessCount}";
    }
}