namespace TecSight.Core.Models;

/// <summary>CPU 静态信息。</summary>
public sealed record CpuInfo(string? Name, int CoreCount, int LogicalProcessorCount, double? BaseClockGhz, string? Manufacturer);

/// <summary>内存条静态信息（含 SPD 字段，F8）。</summary>
public sealed record MemoryModuleInfo(
    string? CapacityBytes,
    string? Speed,
    string? Manufacturer,
    string? PartNumber,
    string? SerialNumber = null,
    string? MemoryType = null,
    string? ConfiguredClockMhz = null,
    string? ConfiguredVoltageMv = null,
    string? DeviceLocator = null);

/// <summary>磁盘静态信息与健康度。</summary>
public sealed record DiskInfo(string? Model, string? SerialNumber, long? CapacityBytes, StorageHealth? Health);

/// <summary>GPU 静态信息。</summary>
public sealed record GpuInfo(string? Name, long? MemoryBytes, string? DriverVersion);

/// <summary>主板静态信息。</summary>
public sealed record MotherboardInfo(string? Manufacturer, string? Product, string? BiosVersion);

/// <summary>网络适配器静态信息。</summary>
public sealed record NetworkAdapterInfo(string? Name, string? MacAddress, bool? IsPhysical);

/// <summary>电池静态信息。</summary>
public sealed record BatteryInfo(string? DeviceName, double? DesignedCapacityWh, double? FullChargeCapacityWh);

/// <summary>硬件清单（Hardware Inventory）：静态存在的硬件及其静态属性。</summary>
public sealed class HardwareInventory
{
    public string? ComputerName { get; set; }
    public string? OsCaption { get; set; }
    public string? OsVersion { get; set; }
    public IReadOnlyList<CpuInfo> Cpus { get; set; } = [];
    public IReadOnlyList<MemoryModuleInfo> MemoryModules { get; set; } = [];
    public IReadOnlyList<DiskInfo> Disks { get; set; } = [];
    public IReadOnlyList<GpuInfo> Gpus { get; set; } = [];
    public MotherboardInfo? Motherboard { get; set; }
    public IReadOnlyList<NetworkAdapterInfo> NetworkAdapters { get; set; } = [];
    public IReadOnlyList<NetworkConfigInfo> NetworkConfigurations { get; set; } = [];
    public BatteryInfo? Battery { get; set; }
}