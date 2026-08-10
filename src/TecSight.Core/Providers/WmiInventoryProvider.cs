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
        return SafeQuery("SELECT Capacity, Speed, Manufacturer, PartNumber FROM Win32_PhysicalMemory",
            row => new MemoryModuleInfo(GetString(row, "Capacity"), GetString(row, "Speed"), GetString(row, "Manufacturer"), GetString(row, "PartNumber")));
    }

    private static List<DiskInfo> QueryDisks()
    {
        return SafeQuery("SELECT Model, SerialNumber, Size FROM Win32_DiskDrive",
            row => new DiskInfo(GetString(row, "Model"), GetString(row, "SerialNumber"), GetLong(row, "Size"), Health: null));
    }

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

    private static BatteryInfo? QueryBattery()
    {
        var battery = SafeQuery("SELECT Name, DesignCapacity FROM Win32_Battery",
            row => new BatteryInfo(GetString(row, "Name"), ToWh(GetInt(row, "DesignCapacity")), null)).FirstOrDefault();
        if (battery is null) return null;
        var full = SafeQuery("root\\wmi", "SELECT FullyChargedCapacity FROM BatteryFullChargedCapacity",
            row => ToWh(GetInt(row, "FullyChargedCapacity"))).FirstOrDefault();
        return battery with { FullChargeCapacityWh = full };
    }

    private static double? ToWh(int? mWh) => mWh is int v && v > 0 ? v / 1000.0 : null;

    // ---- helpers ----
    private static List<T> SafeQuery<T>(string query, Func<ManagementBaseObject, T> map)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(query);
            return searcher.Get().Cast<ManagementBaseObject>().Select(map).ToList();
        }
        catch
        {
            return [];
        }
    }

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

    private static int? GetInt(ManagementBaseObject o, string p)
    {
        try { return Convert.ToInt32(o[p]); } catch { return null; }
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