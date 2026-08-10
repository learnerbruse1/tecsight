using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>
/// 默认快照采集器。任一数据源抛错时，对应部分降级为空/不可用，整体不崩溃（降级）。
/// </summary>
public sealed class SnapshotCollector : ISnapshotCollector
{
    private readonly IHardwareInventoryProvider _inventoryProvider;
    private readonly ILiveMetricsProvider _metricsProvider;
    private readonly ISensorProvider _sensorProvider;

    public SnapshotCollector(
        IHardwareInventoryProvider inventoryProvider,
        ILiveMetricsProvider metricsProvider,
        ISensorProvider sensorProvider)
    {
        _inventoryProvider = inventoryProvider;
        _metricsProvider = metricsProvider;
        _sensorProvider = sensorProvider;
    }

    public Snapshot Collect()
    {
        var inventory = TryCaptureInventory();
        var metrics = TryCaptureMetrics();
        var sensors = TryCaptureSensors();
        var smart = _sensorProvider is ISmartProvider sp ? TryCaptureSmart(sp) : [];
        return new Snapshot(DateTimeOffset.Now, inventory, metrics with { Sensors = sensors, SmartAttributes = smart });
    }

    private HardwareInventory TryCaptureInventory()
    {
        try
        {
            return _inventoryProvider.Capture() ?? new HardwareInventory();
        }
        catch
        {
            return new HardwareInventory();
        }
    }

    private LiveMetrics TryCaptureMetrics()
    {
        try
        {
            return _metricsProvider.Capture() ?? new LiveMetrics { Timestamp = DateTimeOffset.Now };
        }
        catch
        {
            return new LiveMetrics { Timestamp = DateTimeOffset.Now };
        }
    }

    private IReadOnlyList<SensorReading> TryCaptureSensors()
    {
        try
        {
            return _sensorProvider.Capture() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private IReadOnlyList<SmartAttributeReading> TryCaptureSmart(ISmartProvider provider)
    {
        try
        {
            return provider.CaptureSmart() ?? [];
        }
        catch
        {
            return [];
        }
    }
}