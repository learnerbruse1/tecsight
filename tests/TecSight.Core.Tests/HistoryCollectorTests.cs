using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class HistoryCollectorTests
{
    private sealed class DummyCollector : ISnapshotCollector
    {
        public Snapshot Collect() => new(
            DateTimeOffset.Now,
            new HardwareInventory(),
            new LiveMetrics { Timestamp = DateTimeOffset.Now, CpuUsagePercent = 1 });
    }

    [Fact]
    public void Collect_AppendsMetricsToHistory()
    {
        var collector = new HistoryCollector(new DummyCollector(), capacity: 10);

        collector.Collect();
        collector.Collect();
        collector.Collect();

        Assert.Equal(3, collector.History.Count);
        Assert.All(collector.History, m => Assert.Equal(1, m.CpuUsagePercent));
    }

    [Fact]
    public void Collect_TrimsHistoryToCapacity()
    {
        var collector = new HistoryCollector(new DummyCollector(), capacity: 3);

        for (var i = 0; i < 5; i++)
        {
            collector.Collect();
        }

        Assert.Equal(3, collector.History.Count);
        Assert.All(collector.History, m => Assert.Equal(1, m.CpuUsagePercent));
    }
}