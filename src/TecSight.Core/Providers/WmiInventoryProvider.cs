using System.Management;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>
/// 硬件清单真实数据源：WMI。任一查询失败时对应类别降级为空/缺失（降级）。
/// </summary>
public sealed class WmiInventoryProvider : IHardwareInventoryProvider
{
    public string Name => "wmi-inventory";

    public HardwareInventory Capture()
    {
        var inv = new HardwareInventory { ComputerName = SafeString(() => Environment.MachineName) };
        inv.OsCaption = QueryFirstString("SELECT Caption FROM Win32_OperatingSystem", "Caption");
        inv.OsVersion = QueryFirstString("SELECT Version FROM Win32_OperatingSystem", "Version");
        inv.OsArchitecture = QueryFirstString("SELECT OSArchitecture FROM Win32_OperatingSystem", "OSArchitecture");
        inv.OsInstallDate = FormatCimDate(QueryFirstString("SELECT InstallDate FROM Win32_OperatingSystem", "InstallDate"));
        inv.LastBootTime = FormatCimDate(QueryFirstString("SELECT LastBootUpTime FROM Win32_OperatingSystem", "LastBootUpTime"));
        inv.FirmwareType = SafeString(() => Environment.GetEnvironmentVariable("firmware_type")?.Trim() is { Length: > 0 } f ? f : null);
        inv.Cpus = QueryCpus();
        inv.MemoryModules = QueryMemory();
        inv.Disks = QueryDisks();
        inv.Gpus = QueryGpus();
        inv.Motherboard = QueryMotherboard();
        inv.NetworkAdapters = QueryNetwork();
        inv.NetworkConfigurations = QueryNetworkConfig();
        inv.Battery = QueryBattery();
        inv.Displays = QueryDisplays();
        inv.AudioDevices = QueryAudio();
        inv.UsbDevices = QueryUsb();
        inv.Keyboards = QueryKeyboards();
        inv.PointingDevices = QueryPointing();
        inv.Printers = QueryPrinters();
        return inv;
    }

    private static List<CpuInfo> QueryCpus()
    {
        return SafeQuery(
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, Manufacturer, Architecture, SocketDesignation, L2CacheSize, L3CacheSize, CurrentClockSpeed, ProcessorId FROM Win32_Processor",
            row => new CpuInfo(
                GetString(row, "Name"),
                GetInt(row, "NumberOfCores") ?? 0,
                GetInt(row, "NumberOfLogicalProcessors") ?? 0,
                GetInt(row, "MaxClockSpeed") is int mhz && mhz > 0 ? mhz / 1000.0 : null,
                GetString(row, "Manufacturer"),
                ArchitectureName(GetInt(row, "Architecture")),
                GetString(row, "SocketDesignation"),
                GetInt(row, "L2CacheSize"),
                GetInt(row, "L3CacheSize"),
                GetInt(row, "CurrentClockSpeed"),
                GetString(row, "ProcessorId")));
    }

    private static string? ArchitectureName(int? a) => a switch
    {
        0 => "x86",
        5 => "ARM",
        9 => "x64",
        12 => "ARM64",
        _ => a.HasValue ? $"Arch {a}" : null,
    };

    private static List<MemoryModuleInfo> QueryMemory()
    {
        return SafeQuery(
            "SELECT Capacity, Speed, Manufacturer, PartNumber, SerialNumber, SMBIOSMemoryType, ConfiguredClockSpeed, ConfiguredVoltage, DeviceLocator FROM Win32_PhysicalMemory",
            row => new MemoryModuleInfo(
                GetString(row, "Capacity"),
                GetString(row, "Speed"),
                GetString(row, "Manufacturer"),
                GetString(row, "PartNumber"),
                GetString(row, "SerialNumber"),
                MemoryTypeName(GetInt(row, "SMBIOSMemoryType")),
                GetString(row, "ConfiguredClockSpeed"),
                GetInt(row, "ConfiguredVoltage") is int mv && mv > 0 ? $"{mv / 1000.0:0.000} V" : null,
                GetString(row, "DeviceLocator")));
    }

    private static string? MemoryTypeName(int? t) => t switch
    {
        20 => "DDR",
        21 => "DDR2",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _ => t.HasValue ? $"Type {t}" : null,
    };

    private static List<DiskInfo> QueryDisks()
    {
        var disks = SafeQuery("SELECT Model, SerialNumber, Size FROM Win32_DiskDrive",
            row => new DiskInfo(GetString(row, "Model"), GetString(row, "SerialNumber"), GetLong(row, "Size"), Health: null));

        // 磁盘健康/介质/总线/固件：MSFT_PhysicalDisk（无需管理员）
        var storage = SafeQuery("root\\Microsoft\\Windows\\Storage",
            "SELECT DeviceId, FriendlyName, SerialNumber, HealthStatus, Size, MediaType, BusType, FirmwareVersion FROM MSFT_PhysicalDisk",
            row => new
            {
                FriendlyName = GetString(row, "FriendlyName"),
                SerialNumber = GetString(row, "SerialNumber"),
                HealthStatus = GetInt(row, "HealthStatus"),
                MediaType = GetInt(row, "MediaType"),
                BusType = GetInt(row, "BusType"),
                FirmwareVersion = GetString(row, "FirmwareVersion"),
            });
        var result = new List<DiskInfo>(disks.Count);
        foreach (var d in disks)
        {
            var h = storage.FirstOrDefault(x => x.SerialNumber != null && x.SerialNumber.Equals(d.SerialNumber, StringComparison.OrdinalIgnoreCase))
                    ?? storage.FirstOrDefault(x => x.FriendlyName != null && d.Model != null && x.FriendlyName.Contains(d.Model, StringComparison.OrdinalIgnoreCase));
            result.Add(h is null
                ? d
                : d with
                {
                    Health = new StorageHealth(h.FriendlyName ?? d.Model ?? "?", HealthFrom(h.HealthStatus), null),
                    MediaType = MediaTypeName(h.MediaType),
                    BusType = BusTypeName(h.BusType),
                    FirmwareVersion = h.FirmwareVersion,
                });
        }
        return result;
    }

    private static string? MediaTypeName(int? t) => t switch { 3 => "HDD", 4 => "SSD", 5 => "SCM", _ => t.HasValue ? $"Type {t}" : null };
    private static string? BusTypeName(int? t) => t switch
    {
        0 => "Unknown", 1 => "SCSI", 2 => "ATAPI", 3 => "ATA", 4 => "IEEE 1394", 5 => "SSA",
        6 => "Fibre Channel", 7 => "USB", 8 => "RAID", 9 => "iSCSI", 10 => "SAS", 11 => "SATA",
        12 => "SD", 13 => "MMC", 14 => "Virtual", 15 => "File Backed Virtual", 16 => "Storage Spaces", 17 => "NVMe",
        _ => t.HasValue ? $"Bus {t}" : null,
    };

    private static HealthStatus HealthFrom(int? status) => status switch
    {
        0 => HealthStatus.Good,
        1 => HealthStatus.Warning,
        2 => HealthStatus.Critical,
        _ => HealthStatus.Unknown,
    };

    private static List<GpuInfo> QueryGpus()
    {
        return SafeQuery("SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController",
            row => new GpuInfo(GetString(row, "Name"), GetLong(row, "AdapterRAM"), GetString(row, "DriverVersion")));
    }

    private static MotherboardInfo? QueryMotherboard()
    {
        var board = SafeQuery(
            "SELECT Manufacturer, Product FROM Win32_BaseBoard",
            row => new MotherboardInfo(GetString(row, "Manufacturer"), GetString(row, "Product"), null)).FirstOrDefault();
        if (board is null) return null;
        var bios = QueryFirstString("SELECT SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion");
        var biosDate = QueryFirstString("SELECT ReleaseDate FROM Win32_BIOS", "ReleaseDate");
        var sys = SafeQuery("SELECT Manufacturer, Model FROM Win32_ComputerSystem",
            row => new { Manufacturer = GetString(row, "Manufacturer"), Model = GetString(row, "Model") }).FirstOrDefault();
        return board with
        {
            BiosVersion = bios,
            BiosDate = FormatBiosDate(biosDate),
            SystemManufacturer = sys?.Manufacturer,
            SystemModel = sys?.Model,
        };
    }

    private static string? FormatBiosDate(string? d)
    {
        if (string.IsNullOrEmpty(d) || d.Length < 8) return d;
        return $"{d[..4]}-{d.Substring(4, 2)}-{d.Substring(6, 2)}";
    }

    private static List<NetworkAdapterInfo> QueryNetwork()
    {
        var all = SafeQuery(
            "SELECT Name, MACAddress, PhysicalAdapter, NetConnectionStatus, Speed, AdapterType FROM Win32_NetworkAdapter",
            row => new
            {
                Info = new NetworkAdapterInfo(GetString(row, "Name"), GetString(row, "MACAddress"), GetBool(row, "PhysicalAdapter"),
                    NormalizeLinkSpeed(GetLong(row, "Speed")), GetString(row, "AdapterType")),
                Status = GetInt(row, "NetConnectionStatus"),
                Physical = GetBool(row, "PhysicalAdapter"),
            });
        return all
            .Where(x => x.Physical == true || x.Status.HasValue)
            .Select(x => x.Info)
            .Distinct()
            .ToList();
    }

    /// <summary>归一化链路速率：WMI 对"速率未知"返回哨兵值（如 long.MaxValue），<=0 或 >=1Tbps 的异常值也按未知处理。</summary>
    private static long? NormalizeLinkSpeed(long? raw) =>
        raw is long sp && sp > 0 && sp < 1_000_000_000_000 ? sp : null;

    private static List<NetworkConfigInfo> QueryNetworkConfig()
    {
        return SafeQuery(
            "SELECT Description, IPAddress, DefaultIPGateway, DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=TRUE",
            row => new NetworkConfigInfo(
                GetString(row, "Description"),
                GetStringArray(row, "IPAddress"),
                GetStringArray(row, "DefaultIPGateway"),
                GetStringArray(row, "DNSServerSearchOrder")));
    }

    private static List<DisplayInfo> QueryDisplays()
    {
        // 优先 EDID（WmiMonitorID, root\wmi），可拿到真实厂商/生产年份/序列号
        var edid = SafeQuery("root\\wmi", "SELECT UserFriendlyName, ManufacturerName, SerialNumberID, YearOfManufacture FROM WmiMonitorID",
            row => new DisplayInfo(
                DecodeWmiString(row["UserFriendlyName"]),
                DecodeWmiString(row["ManufacturerName"]),
                PnpDeviceId: null,
                SerialNumber: DecodeWmiString(row["SerialNumberID"]),
                ManufactureYear: GetInt(row, "YearOfManufacture")));
        if (edid.Count > 0) return edid;

        return SafeQuery(
            "SELECT Name, MonitorManufacturer, PNPDeviceID FROM Win32_DesktopMonitor",
            row => new DisplayInfo(GetString(row, "Name"), GetString(row, "MonitorManufacturer"), GetString(row, "PNPDeviceID")));
    }

    private static string DecodeWmiString(object? v)
    {
        try
        {
            if (v is Array arr)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var item in arr)
                {
                    var u = Convert.ToUInt16(item);
                    if (u >= 32 && u < 127) sb.Append((char)u);
                }
                return sb.ToString().Trim();
            }
        }
        catch { }
        return "";
    }

    private static List<AudioDeviceInfo> QueryAudio()
    {
        return SafeQuery("SELECT Name, Manufacturer, Status FROM Win32_SoundDevice",
            row => new AudioDeviceInfo(GetString(row, "Name"), GetString(row, "Manufacturer"), GetString(row, "Status")));
    }

    private static List<UsbDeviceInfo> QueryUsb()
    {
        return SafeQuery("SELECT Name, Manufacturer, Status, PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass='USB'",
            row => new UsbDeviceInfo(GetString(row, "Name"), GetString(row, "Manufacturer"), GetString(row, "Status"), GetString(row, "PNPDeviceID")));
    }

    private static List<PnPDeviceInfo> QueryKeyboards() => QueryPnP("SELECT Name, Description, Status FROM Win32_Keyboard");

    private static List<PnPDeviceInfo> QueryPointing() => QueryPnP("SELECT Name, Description, Status FROM Win32_PointingDevice");

    private static List<PnPDeviceInfo> QueryPnP(string wql)
        => SafeQuery(wql, row => new PnPDeviceInfo(GetString(row, "Name"), GetString(row, "Description"), GetString(row, "Status")));

    private static List<PrinterInfo> QueryPrinters()
    {
        return SafeQuery("SELECT Name, DriverName, Default FROM Win32_Printer",
            row => new PrinterInfo(GetString(row, "Name"), GetString(row, "DriverName"), GetBool(row, "Default")));
    }

    private static string? FormatCimDate(string? d)
    {
        if (string.IsNullOrEmpty(d) || d.Length < 14) return d;
        try
        {
            var dt = new DateTime(
                int.Parse(d.Substring(0, 4)), int.Parse(d.Substring(4, 2)), int.Parse(d.Substring(6, 2)),
                int.Parse(d.Substring(8, 2)), int.Parse(d.Substring(10, 2)), int.Parse(d.Substring(12, 2)));
            return dt.ToString("yyyy-MM-dd HH:mm");
        }
        catch
        {
            return d;
        }
    }

    /// <summary>
    /// 电池：容量/化学类型/电压均来自 WMI（mWh → Wh，mV → V）。
    /// </summary>
    private static BatteryInfo? QueryBattery()
    {
        var name = QueryFirstString("SELECT Name FROM Win32_Battery", "Name");
        if (name is null) return null;
        double? designed = SafeQuery("root\\wmi", "SELECT DesignedCapacity FROM BatteryStaticData", row => ToWh(GetDouble(row, "DesignedCapacity"))).FirstOrDefault();
        double? full = SafeQuery("root\\wmi", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity", row => ToWh(GetDouble(row, "FullChargedCapacity"))).FirstOrDefault();
        int? cycles = SafeQuery("root\\wmi", "SELECT CycleCount FROM BatteryCycleCount", row => GetInt(row, "CycleCount")).FirstOrDefault();
        string? chemistry = SafeQuery("root\\wmi", "SELECT Chemistry FROM BatteryStaticData", row => DecodeChemistry(GetDouble(row, "Chemistry"))).FirstOrDefault();
        double? designVoltage = SafeQuery("SELECT DesignVoltage FROM Win32_Battery", row => ToVolt(GetDouble(row, "DesignVoltage"))).FirstOrDefault();
        double? currentVoltage = SafeQuery("root\\wmi", "SELECT Voltage FROM BatteryStatus", row => ToVolt(GetDouble(row, "Voltage"))).FirstOrDefault();
        return new BatteryInfo(name, designed, full, cycles, chemistry, designVoltage, currentVoltage);
    }

    private static double? ToWh(double? mWh) => mWh is double v && v > 0 ? v / 1000.0 : null;
    private static double? ToVolt(double? mV) => mV is double v && v > 0 ? v / 1000.0 : null;

    /// <summary>BatteryStaticData.Chemistry 是 4 字节 ASCII 编码（如 0x50694C = "LiP"）。</summary>
    private static string? DecodeChemistry(double? v)
    {
        if (v is not double d || d <= 0) return null;
        var bytes = BitConverter.GetBytes((uint)d);
        var chars = new List<char>();
        foreach (var b in bytes)
        {
            if (b >= 32 && b < 127) chars.Add((char)b);
            else break;
        }
        return chars.Count >= 2 ? new string(chars.ToArray()) : null;
    }

    // ---- helpers ----
    private static List<T> SafeQuery<T>(string query, Func<ManagementBaseObject, T> map)
        => SafeQuery("root\\cimv2", query, map);

    private static List<T> SafeQuery<T>(string scope, string query, Func<ManagementBaseObject, T> map)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            return searcher.Get().Cast<ManagementBaseObject>().Select(map).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string? QueryFirstString(string query, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            var first = searcher.Get().Cast<ManagementBaseObject>().FirstOrDefault();
            return first is null ? null : GetString(first, property);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(ManagementBaseObject o, string p)
    {
        try { return o[p]?.ToString(); } catch { return null; }
    }

    private static IReadOnlyList<string> GetStringArray(ManagementBaseObject o, string p)
    {
        try
        {
            if (o[p] is string[] arr) return arr.Where(s => !string.IsNullOrEmpty(s)).ToArray();
            return [];
        }
        catch
        {
            return [];
        }
    }

    private static int? GetInt(ManagementBaseObject o, string p)
    {
        try { return Convert.ToInt32(o[p]); } catch { return null; }
    }

    private static double? GetDouble(ManagementBaseObject o, string p)
    {
        try { return Convert.ToDouble(o[p]); } catch { return null; }
    }

    private static long? GetLong(ManagementBaseObject o, string p)
    {
        try { return Convert.ToInt64(o[p]); } catch { return null; }
    }

    private static bool? GetBool(ManagementBaseObject o, string p)
    {
        try { return Convert.ToBoolean(o[p]); } catch { return null; }
    }

    private static string? SafeString(Func<string?> f)
    {
        try { return f(); } catch { return null; }
    }
}