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
public class HistoryCollectorSlimTests
{
    private sealed class SensorDummyCollector : ISnapshotCollector
    {
        public Snapshot Collect() => new(
            DateTimeOffset.Now,
            new HardwareInventory(),
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.Now,
                CpuUsagePercent = 1,
                Sensors =
                [
                    new SensorReading("GPU", "GPU Memory Total", 6141, ""),
                    new SensorReading("GPU", "GPU Memory Used", 1500, ""),
                    new SensorReading("GPU", "GPU Memory Free", 4641, ""),
                    new SensorReading("GPU", "GPU Core", 210, "MHz"),
                    new SensorReading("CPU", "CPU Package", 60, "°C"),
                    new SensorReading("CPU", "CPU Core #1", 12, "%"),
                ],
                SmartAttributes = [new SmartAttributeReading("D", 5, "Reallocated", 100, 100, 10, "0")],
                Processes = [new ProcessUsage("a", 1, 2)],
            });
    }

    [Fact]
    public void Collect_HistoryKeepsOnlyScalarsAndSparkSensors()
    {
        var collector = new HistoryCollector(new SensorDummyCollector(), capacity: 10);

        collector.Collect();
        var h = collector.History.Single();

        Assert.Equal(1, h.CpuUsagePercent);
        // 历史只保留曲线所需的 GPU 传感器（4 个），丢弃其余传感器/SMART/进程
        Assert.Equal(4, h.Sensors.Count);
        Assert.All(h.Sensors, s => Assert.True(s.HardwareName == "GPU"));
        Assert.Empty(h.SmartAttributes);
        Assert.Empty(h.Processes);
    }

    [Fact]
    public void Slim_RemovesHeavyListsButKeepsScalars()
    {
        var m = new LiveMetrics
        {
            Timestamp = DateTimeOffset.Now,
            CpuUsagePercent = 42,
            Sensors = [new SensorReading("GPU", "GPU Core", 210, "MHz"), new SensorReading("CPU", "X", 1, "°C")],
            SmartAttributes = [new SmartAttributeReading("D", 5, "R", 100, 100, 10, "0")],
            Processes = [new ProcessUsage("a", 1, 2)],
        };

        var slim = HistoryCollector.Slim(m);

        Assert.Equal(42, slim.CpuUsagePercent);
        Assert.Single(slim.Sensors);
        Assert.Equal("GPU Core", slim.Sensors[0].SensorName);
        Assert.Empty(slim.SmartAttributes);
        Assert.Empty(slim.Processes);
    }
}