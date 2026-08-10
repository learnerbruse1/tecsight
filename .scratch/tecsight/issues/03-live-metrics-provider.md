# 03 — 运行指标真实数据源（性能计数器+原生 API）

**What to build:** 用性能计数器与原生 API 采集 CPU 占用、内存占用、磁盘 I/O、网络吞吐、GPU 占用，免管理员权限，读取失败时优雅降级。

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] CPU/内存/磁盘/网络/GPU 占用可在本机采集到真实值
- [ ] 免管理员权限
- [ ] 单类指标失败不影响其余
- [ ] 数据进入统一 Snapshot 结构
