# 04 — 硬件清单真实数据源（WMI+降级）

**What to build:** 用 WMI 采集 CPU/内存/磁盘/GPU/主板/网卡/电池的静态清单；WMI 不可用时降级（部分字段缺失或显示不可用）。

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] 主要硬件类别静态信息可采集
- [ ] WMI 失败时优雅降级而非崩溃
- [ ] 清单结构使用领域模型
