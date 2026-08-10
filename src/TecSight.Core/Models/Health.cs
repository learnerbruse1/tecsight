namespace TecSight.Core.Models;

/// <summary>健康度（Health）：设备自诊断给出的健康结论。</summary>
public enum HealthStatus
{
    Unknown,
    Good,
    Warning,
    Critical,
}

/// <summary>存储设备健康度结论（如磁盘 SMART）。</summary>
public sealed record StorageHealth(string DeviceName, HealthStatus Status, string? Message);