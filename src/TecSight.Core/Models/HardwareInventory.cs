namespace TecSight.Core.Models;

/// <summary>CPU 静态信息（含架构/缓存/插槽等扩展字段）。</summary>
public sealed record CpuInfo(
    string? Name,
    int CoreCount,
    int LogicalProcessorCount,
    double? BaseClockGhz,
    string? Manufacturer,
    string? Architecture = null,
    string? SocketDesignation = null,
    int? L2CacheKb = null,
    int? L3CacheKb = null,
    double? CurrentClockMhz = null,
    string? ProcessorId = null);

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

/// <summary>磁盘静态信息与健康度（含介质/总线/固件）。</summary>
public sealed record DiskInfo(
    string? Model,
    string? SerialNumber,
    long? CapacityBytes,
    StorageHealth? Health,
    string? MediaType = null,
    string? BusType = null,
    string? FirmwareVersion = null);

/// <summary>GPU 静态信息。</summary>
public sealed record GpuInfo(string? Name, long? MemoryBytes, string? DriverVersion);

/// <summary>主板静态信息（含系统型号与 BIOS 日期）。</summary>
public sealed record MotherboardInfo(
    string? Manufacturer,
    string? Product,
    string? BiosVersion,
    string? BiosDate = null,
    string? SystemManufacturer = null,
    string? SystemModel = null);

/// <summary>网络适配器静态信息（含速率/类型）。</summary>
public sealed record NetworkAdapterInfo(string? Name, string? MacAddress, bool? IsPhysical, long? SpeedBps = null, string? AdapterType = null);

/// <summary>电池静态信息（含循环次数）。</summary>
public sealed record BatteryInfo(string? DeviceName, double? DesignedCapacityWh, double? FullChargeCapacityWh, int? CycleCount = null);

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
    public IReadOnlyList<DisplayInfo> Displays { get; set; } = [];
    public IReadOnlyList<AudioDeviceInfo> AudioDevices { get; set; } = [];
    public IReadOnlyList<UsbDeviceInfo> UsbDevices { get; set; } = [];
}