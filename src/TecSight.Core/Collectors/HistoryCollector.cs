using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>运行指标环形缓冲：按容量裁剪，保留最近 N 个采样点。</summary>
public sealed class LiveMetricsHistory
{
    private readonly int _capacity;
    private readonly Queue<LiveMetrics> _items = new();

    public LiveMetricsHistory(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "容量必须大于 0");
        _capacity = capacity;
    }

    public void Add(LiveMetrics metrics)
    {
        _items.Enqueue(metrics);
        while (_items.Count > _capacity)
        {
            _items.Dequeue();
        }
    }

    public IReadOnlyList<LiveMetrics> Snapshots => _items.ToArray();
}

/// <summary>带历史的快照采集装饰器：每次采集后把运行指标追加到环形缓冲。</summary>
public sealed class HistoryCollector : ISnapshotCollector
{
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
        _history.Add(snapshot.Metrics);
        return snapshot;
    }
}