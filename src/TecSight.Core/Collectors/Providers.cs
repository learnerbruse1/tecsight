using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>运行指标数据源。实现须自备降级，但采集器仍会兜底捕获异常。</summary>
public interface ILiveMetricsProvider
{
    string Name { get; }
    LiveMetrics Capture();
}

/// <summary>硬件清单数据源。实现须自备降级，但采集器仍会兜底捕获异常。</summary>
public interface IHardwareInventoryProvider
{
    string Name { get; }
    HardwareInventory Capture();
}

/// <summary>传感器读数数据源。实现须自备降级，但采集器仍会兜底捕获异常。</summary>
public interface ISensorProvider
{
    string Name { get; }
    IReadOnlyList<SensorReading> Capture();
}