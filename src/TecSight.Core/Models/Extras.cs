namespace TecSight.Core.Models;

/// <summary>进程占用（F4）：CPU 百分比与内存占用。</summary>
public sealed record ProcessUsage(string Name, double? CpuPercent, long? WorkingSetBytes);

/// <summary>GPU 引擎占用（F6）：3D / Video / Copy / Compute 等各自利用率。</summary>
public sealed record GpuEngineUsage(string EngineType, double Percent);

/// <summary>网络配置 / IP 信息（F10）。</summary>
public sealed record NetworkConfigInfo(string? Description, IReadOnlyList<string> IpAddresses, IReadOnlyList<string> Gateways, IReadOnlyList<string> DnsServers);

/// <summary>磁盘 SMART 属性（F7）：单个属性（ID、名称、当前值、最差值、阈值、原始值）。</summary>
public sealed record SmartAttributeReading(string DiskName, byte Id, string Name, double? CurrentValue, byte? Worst, byte? Threshold, string RawValue);