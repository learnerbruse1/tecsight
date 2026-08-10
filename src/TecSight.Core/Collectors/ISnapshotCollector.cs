using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>快照采集服务：聚合全部数据源，输出规范化快照。</summary>
public interface ISnapshotCollector
{
    Snapshot Collect();
}