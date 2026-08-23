using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>运行指标环形缓冲：按容量裁剪，保留最近 N 个采样点。线程安全。</summary>
public sealed class LiveMetricsHistory
{
    private readonly int _capacity;
    private readonly Queue<LiveMetrics> _items = new();
    private readonly object _gate = new();

    public LiveMetricsHistory(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "容量必须大于 0");
        _capacity = capacity;
    }

    public void Add(LiveMetrics metrics)
    {
        lock (_gate)
        {
            _items.Enqueue(metrics);
            while (_items.Count > _capacity)
            {
                _items.Dequeue();
            }
        }
    }

    public IReadOnlyList<LiveMetrics> Snapshots
    {
        get
        {
            lock (_gate)
            {
                return _items.ToArray();
            }
        }
    }
}

/// <summary>带历史的快照采集装饰器：每次采集后把运行指标追加到环形缓冲。</summary>
public sealed class HistoryCollector : ISnapshotCollector
{
    /// <summary>历史中保留的传感器（仅曲线所需，避免 3600 样本 × 全部传感器导致内存膨胀）。</summary>
    private static readonly HashSet<string> SparkSensorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "GPU Memory Total", "GPU Memory Used", "GPU Memory Free", "GPU Core",
    };

    private readonly ISnapshotCollector _inner;
    private readonly LiveMetricsHistory _history;

    public HistoryCollector(ISnapshotCollector inner, int capacity = 3600)
    {
        _inner = inner;
        _history = new LiveMetricsHistory(capacity);
    }

    public IReadOnlyList<LiveMetrics> History => _history.Snapshots;

    public Snapshot Collect()
    {
        var snapshot = _inner.Collect();
        _history.Add(Slim(snapshot.Metrics));
        return snapshot;
    }

    /// <summary>去掉历史中不需要的列表（完整传感器/SMART/进程），只留标量与曲线所需 GPU 传感器。</summary>
    public static LiveMetrics Slim(LiveMetrics m) => m with
    {
        Sensors = (m.Sensors ?? []).Where(s => SparkSensorNames.Contains(s.SensorName)).ToList(),
        SmartAttributes = [],
        Processes = [],
    };
}
