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
    private static readonly HashSet<string> PeripheralClasses =
    [
        "USB", "USBDevice", "USBHub", "HIDClass", "Keyboard", "Mouse", "Camera",
        "MEDIA", "AudioEndpoint", "Monitor", "Bluetooth", "PrintQueue", "Image", "DiskDrive",
    ];

    private static void ScanPnP(List<PeripheralDevice> list)
    {
        var seen = new HashSet<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Description, Manufacturer, PNPClass, Status, PNPDeviceID FROM Win32_PnPEntity");
            foreach (var o in searcher.Get())
            {
                var name = GetString(o, "Name");
                if (string.IsNullOrWhiteSpace(name)) continue;
                var desc = GetString(o, "Description");
                var mfr = GetString(o, "Manufacturer");
                var cls = GetString(o, "PNPClass");
                if (string.IsNullOrEmpty(cls) || !PeripheralClasses.Contains(cls)) continue; // 只留外设
                if (cls == "HIDClass" && IsInternalHid(name, desc)) continue;               // 去掉系统 HID 内部件
                var cat = Classify(cls, name, desc);
                var key = cat + "|" + name + "|" + mfr;
                if (!seen.Add(key)) continue; // 去重
                list.Add(new PeripheralDevice(
                    name, mfr, desc, cat, cls, Detail: null,
                    Status: GetString(o, "Status"),
                    PnpDeviceId: GetString(o, "PNPDeviceID")));
            }
        }
        catch
        {
            // 降级：忽略
        }
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
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Model, InterfaceType, Size FROM Win32_DiskDrive WHERE InterfaceType='USB'");
            foreach (var o in searcher.Get())
            {
                var model = GetString(o, "Model");
                var size = GetUInt64(o, "Size");
                list.Add(new PeripheralDevice(
                    model,
                    Manufacturer: null,
                    Description: "USB Disk",
                    Category: "storage",
                    PnpClass: "USB",
                    Detail: size is ulong s && s > 0 ? FormatBytes(s) : null));
            }
        }
        catch
        {
            // 降级
        }
    }

    private static void ScanRemovableDisks(List<PeripheralDevice> list)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Size, FreeSpace, FileSystem FROM Win32_LogicalDisk WHERE DriveType=2");
            foreach (var o in searcher.Get())
            {
                var id = GetString(o, "DeviceID");
                var size = GetUInt64(o, "Size");
                var free = GetUInt64(o, "FreeSpace");
                var fs = GetString(o, "FileSystem");
                list.Add(new PeripheralDevice(
                    id,
                    Manufacturer: null,
                    Description: "Removable Disk",
                    Category: "storage",
                    PnpClass: "LogicalDisk",
                    Detail: $"{id}  {FormatBytes(size ?? 0)}  free {FormatBytes(free ?? 0)}  {fs}"));
            }
        }
        catch
        {
            // 降级
        }
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
            case "Net":
                return n.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) ? "bluetooth" : "network";
            case "Bluetooth": return "bluetooth";
            case "DiskDrive": return "storage";
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

    private static string FormatBytes(double b)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var i = 0;
        while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
        return $"{b:0.##} {units[i]}";
    }

    private static string? GetString(ManagementBaseObject o, string p)
    {
        try { return o[p]?.ToString(); } catch { return null; }
    }

    private static ulong? GetUInt64(ManagementBaseObject o, string p)
    {
        try { return Convert.ToUInt64(o[p]); } catch { return null; }
    }
}