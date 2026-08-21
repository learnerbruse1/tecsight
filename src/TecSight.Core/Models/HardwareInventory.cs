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
    string? ProcessorId = null,
    bool? VirtualizationFirmwareEnabled = null,
    bool? VmmExtensions = null);

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
    string? DeviceLocator = null,
    string? FormFactor = null,
    bool? Ecc = null);

/// <summary>磁盘静态信息与健康度（含介质/总线/固件）。</summary>
public sealed record DiskInfo(
    string? Model,
    string? SerialNumber,
    long? CapacityBytes,
    StorageHealth? Health,
    string? MediaType = null,
    string? BusType = null,
    string? FirmwareVersion = null);

/// <summary>GPU 静态信息（含驱动日期/当前分辨率/刷新率/视频模式等）。</summary>
public sealed record GpuInfo(
    string? Name,
    long? MemoryBytes,
    string? DriverVersion,
    string? DriverDate = null,
    int? CurrentHorizontalResolution = null,
    int? CurrentVerticalResolution = null,
    int? CurrentRefreshRate = null,
    string? VideoModeDescription = null,
    string? AdapterCompatibility = null,
    string? VideoProcessor = null,
    string? VideoArchitecture = null);

/// <summary>主板静态信息（含系统型号与 BIOS 日期）。</summary>
public sealed record MotherboardInfo(
    string? Manufacturer,
    string? Product,
    string? BiosVersion,
    string? BiosDate = null,
    string? SystemManufacturer = null,
    string? SystemModel = null);

/// <summary>BIOS / UEFI 固件静态信息（只读）。</summary>
public sealed record BiosInfo(
    string? Manufacturer,
    string? Name,
    string? Version,
    string? SmbiosVersion,
    string? ReleaseDate,
    string? SerialNumber,
    string? Description = null,
    string? BuildNumber = null,
    string? IdentificationCode = null,
    string? LanguageEdition = null,
    int? EmbeddedControllerMajorVersion = null,
    int? EmbeddedControllerMinorVersion = null,
    int? SystemBiosMajorVersion = null,
    int? SystemBiosMinorVersion = null,
    bool? PrimaryBios = null,
    string? Status = null);

/// <summary>网络适配器（物理接口）静态信息（含速率/类型/连接状态/制造商/PNP ID/索引）。</summary>
public sealed record NetworkAdapterInfo(
    string? Name,
    string? MacAddress,
    bool? IsPhysical,
    long? SpeedBps = null,
    string? AdapterType = null,
    string? Manufacturer = null,
    string? PnpDeviceId = null,
    int? NetConnectionStatus = null,
    int? Index = null,
    string? NetConnectionId = null,
    string? DriverVersion = null,
    string? DriverDate = null);

/// <summary>电池静态信息（含循环次数）。</summary>
public sealed record BatteryInfo(string? DeviceName, double? DesignedCapacityWh, double? FullChargeCapacityWh, int? CycleCount = null, string? Chemistry = null, double? DesignVoltageV = null, double? CurrentVoltageV = null);

/// <summary>逻辑磁盘 / 分区信息。</summary>
public sealed record LogicalDiskInfo(
    string? DeviceId,
    string? VolumeName,
    string? FileSystem,
    long? TotalBytes,
    long? FreeBytes,
    int? DriveType);

/// <summary>内存拓扑：插槽占用与最大支持容量、错误校正方式。</summary>
public sealed record MemoryTopologyInfo(
    int? TotalSlots,
    int? UsedSlots,
    long? MaxCapacityBytes,
    string? ErrorCorrection);

/// <summary>系统与安全相关信息（域、时区、安全启动、TPM、Hypervisor 等）。</summary>
public sealed record SystemDetails(
    string? Domain,
    bool? PartOfDomain,
    string? TimeZone,
    bool? SecureBoot,
    string? TpmVersion,
    bool? HypervisorPresent,
    string? SystemType,
    string? SerialNumber = null,
    string? Uuid = null,
    string? ProductName = null,
    string? ProductVersion = null,
    int? VirtualizationBasedSecurityStatus = null,
    bool? MemoryIntegrityEnabled = null,
    int? CodeIntegrityStatus = null);

/// <summary>存在驱动错误/无法正常工作的问题设备。</summary>
public sealed record ProblemDeviceInfo(
    string? Name,
    string? DeviceId,
    string? PnpClass,
    int? ErrorCode,
    string? ErrorDescription,
    string? Status);

/// <summary>Wi-Fi 接口连接详情（SSID/信号/信道/速率等）。</summary>
public sealed record WifiInterfaceInfo(
    string? Name,
    string? State,
    string? Ssid,
    string? Bssid,
    string? RadioType,
    string? Authentication,
    int? Channel,
    double? SignalPercent,
    double? ReceiveRateMbps,
    double? TransmitRateMbps,
    string? ConnectionMode);

/// <summary>硬件清单（Hardware Inventory）：静态存在的硬件及其静态属性。</summary>
public sealed class HardwareInventory
{
    public string? ComputerName { get; set; }
    public string? OsCaption { get; set; }
    public string? OsVersion { get; set; }
    public string? OsArchitecture { get; set; }
    public string? OsInstallDate { get; set; }
    public string? LastBootTime { get; set; }
    public string? FirmwareType { get; set; }
    public IReadOnlyList<CpuInfo> Cpus { get; set; } = [];
    public IReadOnlyList<MemoryModuleInfo> MemoryModules { get; set; } = [];
    public IReadOnlyList<DiskInfo> Disks { get; set; } = [];
    public IReadOnlyList<GpuInfo> Gpus { get; set; } = [];
    public MotherboardInfo? Motherboard { get; set; }
    public BiosInfo? Bios { get; set; }
    public IReadOnlyList<NetworkAdapterInfo> NetworkAdapters { get; set; } = [];
    public IReadOnlyList<NetworkConfigInfo> NetworkConfigurations { get; set; } = [];
    public IReadOnlyList<LogicalDiskInfo> LogicalDisks { get; set; } = [];
    public MemoryTopologyInfo? MemoryTopology { get; set; }
    public SystemDetails? SystemDetails { get; set; }
    public IReadOnlyList<WifiInterfaceInfo> WifiInterfaces { get; set; } = [];
    public IReadOnlyList<ProblemDeviceInfo> ProblemDevices { get; set; } = [];
    public BatteryInfo? Battery { get; set; }
    public IReadOnlyList<DisplayInfo> Displays { get; set; } = [];
    public IReadOnlyList<AudioDeviceInfo> AudioDevices { get; set; } = [];
    public IReadOnlyList<UsbDeviceInfo> UsbDevices { get; set; } = [];
    public IReadOnlyList<PnPDeviceInfo> Keyboards { get; set; } = [];
    public IReadOnlyList<PnPDeviceInfo> PointingDevices { get; set; } = [];
    public IReadOnlyList<PrinterInfo> Printers { get; set; } = [];
}
