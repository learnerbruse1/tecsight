using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class SnapshotCollectorTests
{
    private sealed class FakeMetricsProvider : ILiveMetricsProvider
    {
        public string Name => "fake-metrics";
        public LiveMetrics Capture() => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            CpuUsagePercent = 12.5,
            MemoryUsagePercent = 40,
        };
    }

    private sealed class ThrowingMetricsProvider : ILiveMetricsProvider
    {
        public string Name => "throwing-metrics";
        public LiveMetrics Capture() => throw new InvalidOperationException("metrics unavailable");
    }

    private sealed class FakeInventoryProvider : IHardwareInventoryProvider
    {
        public string Name => "fake-inventory";
        public HardwareInventory Capture() => new() { ComputerName = "FAKE-PC", Cpus = [new CpuInfo("Fake CPU", 4, 8, 3.2, "FakeCorp")] };
    }

    private sealed class ThrowingInventoryProvider : IHardwareInventoryProvider
    {
        public string Name => "throwing-inventory";
        public HardwareInventory Capture() => throw new InvalidOperationException("inventory unavailable");
    }

    private sealed class FakeSensorProvider : ISensorProvider
    {
        public string Name => "fake-sensors";
        public IReadOnlyList<SensorReading> Capture() => [new SensorReading("CPU", "Temperature", 60, "°C")];
    }

    private sealed class ThrowingSensorProvider : ISensorProvider
    {
        public string Name => "throwing-sensors";
        public IReadOnlyList<SensorReading> Capture() => throw new InvalidOperationException("sensors unavailable");
    }

    [Fact]
    public void Collect_ReturnsSnapshotWithDataFromAllProviders()
    {
        var collector = new SnapshotCollector(new FakeInventoryProvider(), new FakeMetricsProvider(), new FakeSensorProvider());

        var snapshot = collector.Collect();

        Assert.Equal("FAKE-PC", snapshot.Inventory.ComputerName);
        Assert.Equal("Fake CPU", snapshot.Inventory.Cpus[0].Name);
        Assert.Equal(12.5, snapshot.Metrics.CpuUsagePercent);
        Assert.Equal(40, snapshot.Metrics.MemoryUsagePercent);
        Assert.Equal(60, snapshot.Metrics.Sensors[0].Value);
        Assert.True((DateTimeOffset.UtcNow - snapshot.CapturedAt).Duration() < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Collect_WhenMetricsProviderThrows_ReturnsEmptyMetricsAndOthersIntact()
    {
        var collector = new SnapshotCollector(new FakeInventoryProvider(), new ThrowingMetricsProvider(), new FakeSensorProvider());

        var snapshot = collector.Collect();

        Assert.Null(snapshot.Metrics.CpuUsagePercent);
        Assert.Equal("FAKE-PC", snapshot.Inventory.ComputerName);
        Assert.Equal(60, snapshot.Metrics.Sensors[0].Value);
    }

    [Fact]
    public void Collect_WhenInventoryProviderThrows_ReturnsEmptyInventoryAndOthersIntact()
    {
        var collector = new SnapshotCollector(new ThrowingInventoryProvider(), new FakeMetricsProvider(), new FakeSensorProvider());

        var snapshot = collector.Collect();

        Assert.Empty(snapshot.Inventory.Cpus);
        Assert.Equal(12.5, snapshot.Metrics.CpuUsagePercent);
        Assert.Equal(60, snapshot.Metrics.Sensors[0].Value);
    }

    [Fact]
    public void Collect_WhenSensorProviderThrows_ReturnsNoSensorsAndOthersIntact()
    {
        var collector = new SnapshotCollector(new FakeInventoryProvider(), new FakeMetricsProvider(), new ThrowingSensorProvider());

        var snapshot = collector.Collect();

        Assert.Empty(snapshot.Metrics.Sensors);
        Assert.Equal("FAKE-PC", snapshot.Inventory.ComputerName);
        Assert.Equal(12.5, snapshot.Metrics.CpuUsagePercent);
    }
}