using System.Management;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>
/// 外围设备探测：枚举即插即用设备 + USB 磁盘 + 可移动磁盘，并按设备类型分类。
/// 不要求全面——只求知道"大概是什么类型的设备"，能拿到的详细信息尽量带上。
/// </summary>
public static class PeripheralProbe
{
    public static IReadOnlyList<PeripheralDevice> Scan()
    {
        var list = new List<PeripheralDevice>();
        ScanPnP(list);
        ScanUsbDisks(list);
        ScanRemovableDisks(list);
        return list;
    }

    /// <summary>只枚举外设相关类别，排除系统内部设备（System/Firmware/网络虚拟端口等）。</summary>
    // 内置硬盘（DiskDrive）不属于"外设"，由磁盘页展示；USB 移动存储由 ScanUsbDisks/ScanRemovableDisks 单独加入。
    private static readonly HashSet<string> PeripheralClasses =
    [
        "USB", "USBDevice", "USBHub", "HIDClass", "Keyboard", "Mouse", "Camera",
        "MEDIA", "AudioEndpoint", "Monitor", "Bluetooth", "PrintQueue", "Image",
    ];

    private static void ScanPnP(List<PeripheralDevice> list)
    {
        var seen = new HashSet<string>();
        var devices = SafeQuery("root\\cimv2",
            "SELECT Name, Description, Manufacturer, PNPClass, Status, PNPDeviceID, DriverProvider, DriverVersion, DriverDate, Service, DeviceID, ConfigManagerErrorCode, HardWareID FROM Win32_PnPEntity",
            o =>
            {
                var name = GetString(o, "Name");
                if (string.IsNullOrWhiteSpace(name)) return null;
                var desc = GetString(o, "Description");
                var mfr = GetString(o, "Manufacturer");
                var cls = GetString(o, "PNPClass");
                if (string.IsNullOrEmpty(cls) || !PeripheralClasses.Contains(cls)) return null; // 只留外设
                if (cls == "HIDClass" && IsInternalHid(name, desc)) return null;               // 去掉系统 HID 内部件
                var cat = Classify(cls, name, desc);
                var key = cat + "|" + name + "|" + mfr;
                if (!seen.Add(key)) return null; // 去重
                return new PeripheralDevice(name, mfr, desc, cat, cls, Detail: null,
                    Status: GetString(o, "Status"),
                    PnpDeviceId: GetString(o, "PNPDeviceID"),
                    DriverProvider: GetString(o, "DriverProvider"),
                    DriverVersion: GetString(o, "DriverVersion"),
                    DriverDate: FormatCimDate(GetString(o, "DriverDate")),
                    Service: GetString(o, "Service"),
                    DeviceId: GetString(o, "DeviceID"),
                    ConfigManagerErrorCode: GetInt(o, "ConfigManagerErrorCode"),
                    HardwareId: GetFirstString(o, "HardWareID"));
            });
        list.AddRange(devices.Where(d => d is not null)!);
    }

    private static bool IsInternalHid(string? name, string? desc)
    {
        var n = (name ?? "") + " " + (desc ?? "");
        return n.Contains("系统", StringComparison.OrdinalIgnoreCase)
               || n.Contains("System", StringComparison.OrdinalIgnoreCase)
               || n.Contains("无线通信", StringComparison.OrdinalIgnoreCase)
               || n.Contains("Configuration", StringComparison.OrdinalIgnoreCase);
    }

    private static void ScanUsbDisks(List<PeripheralDevice> list)
    {
        list.AddRange(SafeQuery("root\\cimv2",
            "SELECT Model, Size FROM Win32_DiskDrive WHERE InterfaceType='USB'",
            o =>
            {
                var size = GetUInt64(o, "Size");
                return new PeripheralDevice(GetString(o, "Model"), Manufacturer: null, Description: "USB Disk",
                    Category: "storage", PnpClass: "USB",
                    Detail: size is ulong s && s > 0 ? FormatUtil.Bytes(s, "") : null);
            }));
    }

    private static void ScanRemovableDisks(List<PeripheralDevice> list)
    {
        list.AddRange(SafeQuery("root\\cimv2",
            "SELECT DeviceID, Size, FreeSpace, FileSystem FROM Win32_LogicalDisk WHERE DriveType=2",
            o =>
            {
                var id = GetString(o, "DeviceID");
                var size = GetUInt64(o, "Size");
                var free = GetUInt64(o, "FreeSpace");
                var fs = GetString(o, "FileSystem");
                return new PeripheralDevice(id, Manufacturer: null, Description: "Removable Disk",
                    Category: "storage", PnpClass: "LogicalDisk",
                    Detail: $"{id}  {FormatUtil.Bytes(size ?? 0, "")}  free {FormatUtil.Bytes(free ?? 0, "")}  {fs}");
            }));
    }

    /// <summary>按 PNPClass/名称/描述推断设备类型键。</summary>
    public static string Classify(string? pnpClass, string? name, string? description)
    {
        var n = (name ?? "") + " " + (description ?? "");
        switch (pnpClass)
        {
            case "Keyboard": return "keyboard";
            case "Mouse": return "mouse";
            case "Camera": return "camera";
            case "Monitor": return "display";
            case "PrintQueue": return "printer";
            case "MEDIA":
            case "AudioEndpoint":
                return "audio";
            case "Bluetooth": return "bluetooth";
            case "USB":
            case "USBDevice":
                if (n.Contains("Hub", StringComparison.OrdinalIgnoreCase) || n.Contains("集线器")) return "hub";
                if (n.Contains("Card Reader", StringComparison.OrdinalIgnoreCase) || n.Contains("读卡器")) return "cardreader";
                if (n.Contains("Phone", StringComparison.OrdinalIgnoreCase) || n.Contains("MTP", StringComparison.OrdinalIgnoreCase)) return "phone";
                if (n.Contains("Storage", StringComparison.OrdinalIgnoreCase) || n.Contains("Disk", StringComparison.OrdinalIgnoreCase) || n.Contains("存储")) return "storage";
                if (n.Contains("Audio", StringComparison.OrdinalIgnoreCase) || n.Contains("耳机") || n.Contains("Headset")) return "audio";
                return "usb";
            case "HIDClass":
                if (n.Contains("Keyboard", StringComparison.OrdinalIgnoreCase) || n.Contains("键盘")) return "keyboard";
                if (n.Contains("Mouse", StringComparison.OrdinalIgnoreCase) || n.Contains("Pointer", StringComparison.OrdinalIgnoreCase) || n.Contains("鼠标")) return "mouse";
                if (n.Contains("Game", StringComparison.OrdinalIgnoreCase) || n.Contains("Joystick", StringComparison.OrdinalIgnoreCase) || n.Contains("手柄")) return "gamepad";
                if (n.Contains("Camera", StringComparison.OrdinalIgnoreCase) || n.Contains("摄像头")) return "camera";
                if (n.Contains("Consumer Control", StringComparison.OrdinalIgnoreCase)) return "input";
                return "input";
            default:
                if (n.Contains("摄像头", StringComparison.OrdinalIgnoreCase) || (n.Contains("Camera", StringComparison.OrdinalIgnoreCase) && pnpClass is "Image" or null)) return "camera";
                if (n.Contains("蓝牙", StringComparison.OrdinalIgnoreCase) || n.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase)) return "bluetooth";
                return "other";
        }
    }

    /// <summary>从 PNP 设备 ID 解析 USB VID/PID（如 USB\VID_1234&PID_5678\...）。</summary>
    public static (string? Vid, string? Pid) ParseUsbVidPid(string? pnpDeviceId)
    {
        if (string.IsNullOrEmpty(pnpDeviceId)) return (null, null);
        string? vid = null, pid = null;
        var vidIdx = pnpDeviceId.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
        if (vidIdx >= 0 && vidIdx + 8 <= pnpDeviceId.Length)
            vid = pnpDeviceId.Substring(vidIdx + 4, 4);
        var pidIdx = pnpDeviceId.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
        if (pidIdx >= 0 && pidIdx + 8 <= pnpDeviceId.Length)
            pid = pnpDeviceId.Substring(pidIdx + 4, 4);
        return (vid, pid);
    }

    private static string? FormatCimDate(string? d)
    {
        if (string.IsNullOrEmpty(d) || d.Length < 8) return d;
        return $"{d[..4]}-{d.Substring(4, 2)}-{d.Substring(6, 2)}";
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


    private static string? GetString(ManagementBaseObject o, string p)
    {
        try { return o[p]?.ToString(); } catch { return null; }
    }

    private static string? GetFirstString(ManagementBaseObject o, string p)
    {
        try
        {
            if (o[p] is string[] arr) return arr.FirstOrDefault(s => !string.IsNullOrEmpty(s));
            return o[p]?.ToString();
        }
        catch { return null; }
    }

    private static int? GetInt(ManagementBaseObject o, string p)
    {
        try { return Convert.ToInt32(o[p]); } catch { return null; }
    }

    private static ulong? GetUInt64(ManagementBaseObject o, string p)
    {
        try { return Convert.ToUInt64(o[p]); } catch { return null; }
    }
}
