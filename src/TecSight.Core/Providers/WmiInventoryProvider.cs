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
        inv.Cpus = QueryCpus();
        inv.MemoryModules = QueryMemory();
        inv.Disks = QueryDisks();
        inv.Gpus = QueryGpus();
        inv.Motherboard = QueryMotherboard();
        inv.NetworkAdapters = QueryNetwork();
        inv.NetworkConfigurations = QueryNetworkConfig();
        inv.Battery = QueryBattery();
        return inv;
    }

    private static List<CpuInfo> QueryCpus()
    {
        return SafeQuery("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, Manufacturer FROM Win32_Processor",
            row => new CpuInfo(
                GetString(row, "Name"),
                GetInt(row, "NumberOfCores") ?? 0,
                GetInt(row, "NumberOfLogicalProcessors") ?? 0,
                GetInt(row, "MaxClockSpeed") is int mhz && mhz > 0 ? mhz / 1000.0 : null,
                GetString(row, "Manufacturer")));
    }

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

        // 磁盘健康度：MSFT_PhysicalDisk（无需管理员，本机验证可用）
        var health = SafeQuery("root\\Microsoft\\Windows\\Storage",
            "SELECT DeviceId, FriendlyName, SerialNumber, HealthStatus, Size, MediaType FROM MSFT_PhysicalDisk",
            row => new
            {
                FriendlyName = GetString(row, "FriendlyName"),
                SerialNumber = GetString(row, "SerialNumber"),
                HealthStatus = GetInt(row, "HealthStatus"),
            });
        var result = new List<DiskInfo>(disks.Count);
        foreach (var d in disks)
        {
            var h = health.FirstOrDefault(x => x.SerialNumber != null && x.SerialNumber.Equals(d.SerialNumber, StringComparison.OrdinalIgnoreCase))
                    ?? health.FirstOrDefault(x => x.FriendlyName != null && d.Model != null && x.FriendlyName.Contains(d.Model, StringComparison.OrdinalIgnoreCase));
            result.Add(h is null
                ? d
                : d with { Health = new StorageHealth(h.FriendlyName ?? d.Model ?? "?", HealthFrom(h.HealthStatus), null) });
        }
        return result;
    }

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
        return board with { BiosVersion = bios };
    }

    private static List<NetworkAdapterInfo> QueryNetwork()
    {
        var all = SafeQuery(
            "SELECT Name, MACAddress, PhysicalAdapter, NetConnectionStatus FROM Win32_NetworkAdapter",
            row => new
            {
                Info = new NetworkAdapterInfo(GetString(row, "Name"), GetString(row, "MACAddress"), GetBool(row, "PhysicalAdapter")),
                Status = GetInt(row, "NetConnectionStatus"),
                Physical = GetBool(row, "PhysicalAdapter"),
            });
        return all
            .Where(x => x.Physical == true || x.Status.HasValue)
            .Select(x => x.Info)
            .Distinct()
            .ToList();
    }

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

    /// <summary>
    /// 电池容量：设计容量来自 root\wmi BatteryStaticData.DesignedCapacity，
    /// 满充容量来自 root\wmi BatteryFullChargedCapacity.FullChargedCapacity（单位 mWh）。
    /// </summary>
    private static BatteryInfo? QueryBattery()
    {
        var name = QueryFirstString("SELECT Name FROM Win32_Battery", "Name");
        if (name is null) return null;
        double? designed = SafeQuery("root\\wmi", "SELECT DesignedCapacity FROM BatteryStaticData", row => ToWh(GetUInt(row, "DesignedCapacity"))).FirstOrDefault();
        double? full = SafeQuery("root\\wmi", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity", row => ToWh(GetUInt(row, "FullChargedCapacity"))).FirstOrDefault();
        return new BatteryInfo(name, designed, full);
    }

    private static double? ToWh(double? mWh) => mWh is double v && v > 0 ? v / 1000.0 : null;

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

    private static double? GetUInt(ManagementBaseObject o, string p)
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