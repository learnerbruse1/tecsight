using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class SnapshotCollectorTests
{
    internal sealed class FakeMetricsProvider : ILiveMetricsProvider
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

    internal sealed class FakeInventoryProvider : IHardwareInventoryProvider
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
public class SnapshotCollectorSmartTests
{
    private sealed class FakeSmartSensorProvider : ISensorProvider, ISmartProvider
    {
        public string Name => "fake-smart";
        public IReadOnlyList<SensorReading> Capture() => [new SensorReading("Disk", "Temperature", 40, "°C")];
        public IReadOnlyList<SmartAttributeReading> CaptureSmart() =>
            [new SmartAttributeReading("Disk1", 5, "Reallocated Sectors", 100, 100, 10, "0")];
    }

    private sealed class ThrowingSmartSensorProvider : ISensorProvider, ISmartProvider
    {
        public string Name => "throwing-smart";
        public IReadOnlyList<SensorReading> Capture() => [new SensorReading("Disk", "Temperature", 40, "°C")];
        public IReadOnlyList<SmartAttributeReading> CaptureSmart() => throw new InvalidOperationException("smart unavailable");
    }

    private sealed class PlainSensorProvider : ISensorProvider
    {
        public string Name => "plain";
        public IReadOnlyList<SensorReading> Capture() => [new SensorReading("Disk", "Temperature", 40, "°C")];
    }

    [Fact]
    public void Collect_MergesSmartAttributesWhenSensorProviderImplementsISmartProvider()
    {
        var collector = new SnapshotCollector(new SnapshotCollectorTests.FakeInventoryProvider(), new SnapshotCollectorTests.FakeMetricsProvider(), new FakeSmartSensorProvider());

        var snapshot = collector.Collect();

        Assert.Single(snapshot.Metrics.SmartAttributes);
        Assert.Equal("Disk1", snapshot.Metrics.SmartAttributes[0].DiskName);
        Assert.Equal(5, snapshot.Metrics.SmartAttributes[0].Id);
        Assert.Equal(40, snapshot.Metrics.Sensors[0].Value);
    }

    [Fact]
    public void Collect_WhenSmartProviderThrows_ReturnsNoSmartAttributesAndOthersIntact()
    {
        var collector = new SnapshotCollector(new SnapshotCollectorTests.FakeInventoryProvider(), new SnapshotCollectorTests.FakeMetricsProvider(), new ThrowingSmartSensorProvider());

        var snapshot = collector.Collect();

        Assert.Empty(snapshot.Metrics.SmartAttributes);
        Assert.Equal(40, snapshot.Metrics.Sensors[0].Value);
        Assert.Equal("FAKE-PC", snapshot.Inventory.ComputerName);
    }

    [Fact]
    public void Collect_WhenSensorProviderIsPlain_NoSmartAttributes()
    {
        var collector = new SnapshotCollector(new SnapshotCollectorTests.FakeInventoryProvider(), new SnapshotCollectorTests.FakeMetricsProvider(), new PlainSensorProvider());

        var snapshot = collector.Collect();

        Assert.Empty(snapshot.Metrics.SmartAttributes);
    }
}
public class SnapshotCollectorNullReturnTests
{
    private sealed class NullMetricsProvider : ILiveMetricsProvider
    {
        public string Name => "null-metrics";
        public LiveMetrics Capture() => null!;
    }

    private sealed class NullSensorProvider : ISensorProvider
    {
        public string Name => "null-sensors";
        public IReadOnlyList<SensorReading> Capture() => null!;
    }

    private sealed class NullInventoryProvider : IHardwareInventoryProvider
    {
        public string Name => "null-inventory";
        public HardwareInventory Capture() => null!;
    }

    [Fact]
    public void Collect_WhenAllProvidersReturnNull_DoesNotCrashAndReturnsEmpty()
    {
        var collector = new SnapshotCollector(new NullInventoryProvider(), new NullMetricsProvider(), new NullSensorProvider());

        var snapshot = collector.Collect();

        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.Inventory);
        Assert.NotNull(snapshot.Metrics);
        Assert.Empty(snapshot.Metrics.Sensors);
        Assert.Empty(snapshot.Inventory.Cpus);
    }

    [Fact]
    public void Collect_WhenSensorProviderReturnsNull_OthersIntact()
    {
        var collector = new SnapshotCollector(new SnapshotCollectorTests.FakeInventoryProvider(), new SnapshotCollectorTests.FakeMetricsProvider(), new NullSensorProvider());

        var snapshot = collector.Collect();

        Assert.Empty(snapshot.Metrics.Sensors);
        Assert.Equal("FAKE-PC", snapshot.Inventory.ComputerName);
        Assert.Equal(12.5, snapshot.Metrics.CpuUsagePercent);
    }
}