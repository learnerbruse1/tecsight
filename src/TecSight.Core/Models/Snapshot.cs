namespace TecSight.Core.Models;

/// <summary>快照（Snapshot）：某一时刻硬件清单与运行指标的完整集合。</summary>
public sealed record Snapshot(DateTimeOffset CapturedAt, HardwareInventory Inventory, LiveMetrics Metrics);