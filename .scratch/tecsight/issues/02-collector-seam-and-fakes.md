# 02 — 采集接缝 + 假数据源 + 降级测试

**What to build:** 定义 ISnapshotCollector 与各数据源接口（运行指标/硬件清单/传感器），HardwareService 聚合；注入假数据源产出完整快照；测试证明某源抛错时该项显示不可用而整体不崩溃。

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] ISnapshotCollector 返回规范化 Snapshot
- [ ] 注入假数据源可产出全部类别数据
- [ ] 任一数据源抛错 → 对应项不可用，其余数据正常
- [ ] 聚合逻辑有测试覆盖
