namespace TecSight.Core.Models;

/// <summary>运行指标（Live Metrics）：随使用实时变化的数值。</summary>
public sealed record LiveMetrics
{
    public DateTimeOffset Timestamp { get; init; }
    public double? CpuUsagePercent { get; init; }
    public double? CpuFrequencyMhz { get; init; }
    public double? MemoryUsagePercent { get; init; }
    public double? MemoryUsedBytes { get; init; }
    public double? MemoryTotalBytes { get; init; }
    public double? GpuUsagePercent { get; init; }
    public double? DiskReadBytesPerSec { get; init; }
    public double? DiskWriteBytesPerSec { get; init; }
    public double? NetworkDownloadBps { get; init; }
    public double? NetworkUploadBps { get; init; }
    public double? BatteryChargePercent { get; init; }
    public bool? BatteryIsCharging { get; init; }
    public double? SystemUptimeSeconds { get; init; }
    public IReadOnlyList<SensorReading> Sensors { get; init; } = [];
    public IReadOnlyList<SmartAttributeReading> SmartAttributes { get; init; } = [];
    public IReadOnlyList<ProcessUsage> Processes { get; init; } = [];
    public int TotalProcessCount { get; init; }
    public IReadOnlyList<GpuEngineUsage> GpuEngines { get; init; } = [];
}