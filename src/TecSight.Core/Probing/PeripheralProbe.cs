using System.Management;
using System.Text;
using Microsoft.Win32;
using Nefarius.Utilities.DeviceManagement.PnP;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>
/// 外围设备探测：枚举即插即用设备 + USB 磁盘 + 可移动磁盘，并按设备类型分类。
/// 不要求全面——只求知道"大概是什么类型的设备"，能拿到的详细信息尽量带上。
/// </summary>
public static class PeripheralProbe
{
    private static readonly System.Management.EnumerationOptions WmiEnumerationOptions = new() { Timeout = TimeSpan.FromSeconds(20) };

    // DEVPKEY_Device_* 的厂商/描述/友好名称键并未在 Nefarius 6.0.0 中以常量暴露，这里按官方 devpkey.h 定义补上。
    // GUID {A45C254E-DF1C-4EFD-8020-67D146A850E0}：DeviceDesc=2、Manufacturer=13、FriendlyName=14。
    private static readonly DevicePropertyKey DevpkeyDeviceDesc =
        CustomDeviceProperty.CreateCustomDeviceProperty(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2, typeof(string));
    private static readonly DevicePropertyKey DevpkeyDeviceManufacturer =
        CustomDeviceProperty.CreateCustomDeviceProperty(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 13, typeof(string));
    private static readonly DevicePropertyKey DevpkeyDeviceFriendlyName =
        CustomDeviceProperty.CreateCustomDeviceProperty(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14, typeof(string));

    private sealed record PnPRow(
        string Name,
        string? Description,
        string? Manufacturer,
        string? PnpClass,
        string? Status,
        string? PnpDeviceId,
        string? Service,
        string? DeviceId,
        int? ConfigManagerErrorCode,
        string? HardwareId);

    private sealed record MonitorIdInfo(string? InstanceName, string? Manufacturer, string? Serial, int? Year);
    private sealed record PnPBackfill(
        string? Manufacturer,
        string? Description,
        string? DriverProvider,
        string? DriverVersion,
        string? DriverDate,
        string? Service,
        string? HardwareId);

    public static IReadOnlyList<PeripheralDevice> Scan()
    {
        var list = new List<PeripheralDevice>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ScanPnP(list, seen);
        ScanHidDevices(list, seen);
        ScanKeyboards(list, seen);
        ScanPointingDevices(list, seen);
        ScanUsbDisks(list);
        ScanRemovableDisks(list);

        var deduped = new List<PeripheralDevice>(list.Count);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in list)
        {
            if (!string.IsNullOrWhiteSpace(device.PnpDeviceId) && !seenIds.Add(device.PnpDeviceId.Trim()))
            {
                continue;
            }
            deduped.Add(device);
        }
        return deduped;
    }

    private static void ScanHidDevices(List<PeripheralDevice> list, HashSet<string> seen)
    {
        try
        {
            foreach (var device in HidSharp.DeviceList.Local.GetHidDevices())
            {
                var name = GetStringSafely(device.GetProductName) ?? "";
                var manufacturer = GetStringSafely(device.GetManufacturer);
                var serial = GetStringSafely(device.GetSerialNumber);
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = $"HID {device.VendorID:X4}:{device.ProductID:X4}";
                }
                if (IsInternalHid(name, "HID Device")) continue;
                var id = device.VendorID != 0 && device.ProductID != 0
                    ? $"USB\\VID_{device.VendorID:X4}&PID_{device.ProductID:X4}"
                    : null;
                var key = "hid|" + name + "|" + (id ?? "");
                if (!seen.Add(key)) continue;
                var category = Classify("HIDClass", name, "HID Device");
                list.Add(new PeripheralDevice(name, manufacturer, "HID Device", category, "HIDClass",
                    Detail: null,
                    Status: "OK",
                    PnpDeviceId: id,
                    SerialNumber: serial));
            }
        }
        catch
        {
            // HID 枚举不可用时降级
        }
    }

    /// <summary>把硬件清单里的“其他设备”类目转换为外设条目，补足 PnP 扫描可能遗漏的设备。</summary>
    public static IReadOnlyList<PeripheralDevice> FromInventory(HardwareInventory inv)
    {
        var list = new List<PeripheralDevice>();
        foreach (var d in inv.Displays)
        {
            var name = string.IsNullOrWhiteSpace(d.Name) ? d.Manufacturer : d.Name;
            if (string.IsNullOrWhiteSpace(name)) continue;
            list.Add(new PeripheralDevice(name, d.Manufacturer, "Display", "display", "Monitor",
                Detail: null,
                Status: "OK",
                PnpDeviceId: d.PnpDeviceId,
                SerialNumber: d.SerialNumber,
                ManufactureYear: d.ManufactureYear));
        }
        foreach (var a in inv.AudioDevices)
        {
            if (string.IsNullOrWhiteSpace(a.Name)) continue;
            list.Add(new PeripheralDevice(a.Name, a.Manufacturer, "Audio", "audio", "MEDIA", Detail: null, Status: a.Status, PnpDeviceId: a.PnpDeviceId));
        }
        foreach (var u in inv.UsbDevices)
        {
            if (string.IsNullOrWhiteSpace(u.Name)) continue;
            var category = Classify("USB", u.Name, null);
            list.Add(new PeripheralDevice(u.Name, u.Manufacturer, "USB", category, "USB", Detail: null, Status: u.Status, PnpDeviceId: u.PnpDeviceId));
        }
        foreach (var n in inv.NetworkAdapters.Where(n =>
                     n.IsPhysical == true
                     || (n.IsPhysical is null && !HardwareClassifier.IsVirtualNetworkAdapter(n.Name, n.AdapterType))
                     || (n.NetConnectionStatus.HasValue && !HardwareClassifier.IsVirtualNetworkAdapter(n.Name, n.AdapterType))))
        {
            if (string.IsNullOrWhiteSpace(n.Name)) continue;
            var detail = new List<string>();
            if (n.SpeedBps is long sp && sp > 0) detail.Add(FormatUtil.LinkSpeed(sp, ""));
            if (!string.IsNullOrWhiteSpace(n.AdapterType)) detail.Add(n.AdapterType);
            list.Add(new PeripheralDevice(
                n.Name,
                n.Manufacturer,
                n.AdapterType,
                "network",
                "Net",
                Detail: string.Join(" · ", detail.Where(x => !string.IsNullOrWhiteSpace(x))),
                Status: n.NetConnectionStatus?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                PnpDeviceId: n.PnpDeviceId,
                DriverProvider: null,
                DriverVersion: n.DriverVersion,
                DriverDate: n.DriverDate));
        }
        foreach (var k in inv.Keyboards)
        {
            if (string.IsNullOrWhiteSpace(k.Name)) continue;
            list.Add(new PeripheralDevice(k.Name, k.Manufacturer, k.Description, "keyboard", "Keyboard", Detail: null, Status: k.Status, PnpDeviceId: k.PnpDeviceId));
        }
        foreach (var m in inv.PointingDevices)
        {
            if (string.IsNullOrWhiteSpace(m.Name)) continue;
            list.Add(new PeripheralDevice(m.Name, m.Manufacturer, m.Description, "mouse", "Mouse", Detail: null, Status: m.Status, PnpDeviceId: m.PnpDeviceId));
        }
        foreach (var p in inv.Printers)
        {
            if (string.IsNullOrWhiteSpace(p.Name)) continue;
            list.Add(new PeripheralDevice(p.Name, null, "Printer", "printer", "PrintQueue", Detail: p.DriverName, Status: p.IsDefault == true ? "Default" : null));
        }
        return list;
    }

    /// <summary>只枚举外设相关类别，排除系统内部设备（System/Firmware/网络虚拟端口等）。</summary>
    // 内置硬盘（DiskDrive）不属于"外设"，由磁盘页展示；USB 移动存储由 ScanUsbDisks/ScanRemovableDisks 单独加入。
    private static readonly HashSet<string> PeripheralClasses =
    [
        "USB", "USBDevice", "USBHub", "HIDClass", "Keyboard", "Mouse", "Camera",
        "MEDIA", "AudioEndpoint", "Monitor", "Bluetooth", "PrintQueue", "Image",
    ];

    private static void ScanPnP(List<PeripheralDevice> list, HashSet<string> seen)
    {
        var monitorInfo = SafeQuery("root\\cimv2",
            "SELECT PNPDeviceID, ScreenWidth, ScreenHeight FROM Win32_DesktopMonitor",
            o => new
            {
                Id = GetString(o, "PNPDeviceID"),
                Width = GetInt(o, "ScreenWidth"),
                Height = GetInt(o, "ScreenHeight"),
            });
        var refreshRates = SafeQuery("root\\cimv2",
            "SELECT CurrentRefreshRate FROM Win32_VideoController",
            o => GetInt(o, "CurrentRefreshRate"))
            .Where(v => v is > 0)
            .ToList();
        var refreshRate = refreshRates.Count == 1 ? refreshRates[0] : (int?)null;

        // 第一遍：Win32_PnPEntity 决定有哪些外设（其驱动/厂商等字段经常为空）。
        var rows = SafeQuery("root\\cimv2",
            "SELECT Name, Description, Manufacturer, PNPClass, Status, PNPDeviceID, Service, DeviceID, ConfigManagerErrorCode, HardwareID FROM Win32_PnPEntity",
            o =>
            {
                var name = GetString(o, "Name");
                if (string.IsNullOrWhiteSpace(name)) return null;
                var desc = GetString(o, "Description");
                var mfr = GetString(o, "Manufacturer");
                var cls = GetString(o, "PNPClass");
                var id = GetString(o, "PNPDeviceID");
                if (string.IsNullOrEmpty(cls) || !PeripheralClasses.Contains(cls)) return null; // 只留外设
                if (cls == "HIDClass" && IsInternalHid(name, desc)) return null;               // 去掉系统 HID 内部件
                return new PnPRow(
                    name, desc, mfr, cls,
                    Status: GetString(o, "Status"),
                    PnpDeviceId: id,
                    Service: GetString(o, "Service"),
                    DeviceId: GetString(o, "DeviceID"),
                    ConfigManagerErrorCode: GetInt(o, "ConfigManagerErrorCode"),
                    HardwareId: GetFirstString(o, "HardwareID"));
            }).Where(r => r is not null).Cast<PnPRow>().ToList();

        // 第二遍：仅在确实需要时取补充数据源，避免无显示器的机器也去触碰 root\wmi。
        var hasDisplay = rows.Any(r => Classify(r.PnpClass, r.Name, r.Description) == "display");
        var monitorIds = hasDisplay ? QueryMonitorIds() : null;

        foreach (var row in rows)
        {
            var cat = Classify(row.PnpClass, row.Name, row.Description);
            var key = cat + "|" + row.Name + "|" + row.Manufacturer + "|" + row.PnpDeviceId;
            if (!seen.Add(key)) continue; // 去重

            var manufacturer = row.Manufacturer;
            var description = row.Description;
            string? resolution = null;
            double? deviceRefreshRate = null;
            string? serial = null;
            int? manufactureYear = null;
            if (cat == "display")
            {
                deviceRefreshRate = refreshRate;
                // 当前分辨率优先取 Win32_DesktopMonitor；拿不到时从注册表 EDID 的首选时序推导。
                var mi = monitorInfo.FirstOrDefault(x =>
                    !string.IsNullOrEmpty(x.Id) && x.Id.Equals(row.PnpDeviceId, StringComparison.OrdinalIgnoreCase));
                if (mi is { Width: int w, Height: int h } && w > 0 && h > 0)
                {
                    resolution = $"{w} × {h}";
                }

                var edid = ReadRegistryEdid(row.PnpDeviceId);
                if (edid is { Length: >= 128 } && IsEdidBlock(edid))
                {
                    if (ParsePreferredMode(edid) is { } mode)
                    {
                        resolution ??= $"{mode.Width} × {mode.Height}";
                        deviceRefreshRate ??= mode.RefreshRate;
                    }
                    // EDID 的 EISA 厂商码比 WMI 的“(标准监视器类型)”更具体，优先采用。
                    manufacturer = DecodeEisaManufacturer(edid) ?? manufacturer;
                    manufactureYear ??= ParseManufactureYear(edid);
                }

                // EDID 字符串里的厂商/序列号/出厂年份（WmiMonitorID），比注册表字节更友好。
                if (monitorIds is not null &&
                    monitorIds.TryGetValue(NormalizeMonitorInstanceName(row.PnpDeviceId), out var monitor))
                {
                    manufacturer = monitor.Manufacturer ?? manufacturer;
                    serial ??= monitor.Serial;
                    manufactureYear ??= monitor.Year;
                }
            }

            // Win32_PnPEntity 的驱动字段与厂商经常为空，这里用 SetupAPI 统一设备属性回填。
            // （Win32_PnPSignedDriver 虽然字段齐全，但一次枚举就要 5 秒以上，故不采用。）
            string? driverProvider = null;
            string? driverVersion = null;
            string? driverDate = null;
            var service = row.Service;
            var hardwareId = row.HardwareId;
            if (manufacturer is null || description is null || driverProvider is null || driverVersion is null
                || driverDate is null || service is null || hardwareId is null)
            {
                var extra = ReadPnPBackfill(row.PnpDeviceId);
                if (extra is not null)
                {
                    manufacturer ??= extra.Manufacturer;
                    description ??= extra.Description;
                    driverProvider ??= extra.DriverProvider;
                    driverVersion ??= extra.DriverVersion;
                    driverDate ??= extra.DriverDate;
                    service ??= extra.Service;
                    hardwareId ??= extra.HardwareId;
                }
            }

            list.Add(new PeripheralDevice(
                row.Name, manufacturer, description, cat, row.PnpClass,
                Detail: null,
                Status: row.Status,
                PnpDeviceId: row.PnpDeviceId,
                DriverProvider: driverProvider,
                DriverVersion: driverVersion,
                DriverDate: driverDate,
                Service: service,
                DeviceId: row.DeviceId,
                ConfigManagerErrorCode: row.ConfigManagerErrorCode,
                HardwareId: hardwareId,
                Resolution: resolution,
                RefreshRate: deviceRefreshRate,
                SerialNumber: serial,
                ManufactureYear: manufactureYear));
        }
    }

    /// <summary>从 WmiMonitorID（root\wmi）取 EDID 字符串中的厂商/序列号/出厂年份。</summary>
    private static Dictionary<string, MonitorIdInfo>? QueryMonitorIds()
    {
        try
        {
            var rows = SafeQuery("root\\wmi",
                "SELECT InstanceName, ManufacturerName, SerialNumberID, YearOfManufacture FROM WmiMonitorID WHERE Active=TRUE",
                o =>
                {
                    var year = GetUInt16(o, "YearOfManufacture");
                    return new MonitorIdInfo(
                        GetString(o, "InstanceName"),
                        DecodeEdidString(GetRaw(o, "ManufacturerName")),
                        DecodeEdidString(GetRaw(o, "SerialNumberID")),
                        year.HasValue && year.Value >= 1990 ? (int?)year.Value : null);
                });
            return rows
                .Where(x => !string.IsNullOrWhiteSpace(x.InstanceName))
                .GroupBy(x => NormalizeMonitorInstanceName(x.InstanceName), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>用 SetupAPI 统一设备属性兜底读取 WMI 拿不到的字段（厂商/描述/驱动/服务/硬件 ID）。</summary>
    private static PnPBackfill? ReadPnPBackfill(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        try
        {
            var device = PnPDevice.GetDeviceByInstanceId(instanceId.Trim());
            return new PnPBackfill(
                Manufacturer: GetStringProperty(device, DevpkeyDeviceManufacturer),
                Description: GetStringProperty(device, DevpkeyDeviceDesc)
                             ?? GetStringProperty(device, DevicePropertyKey.Device_BusReportedDeviceDesc)
                             ?? GetStringProperty(device, DevpkeyDeviceFriendlyName),
                DriverProvider: GetStringProperty(device, DevicePropertyKey.Device_DriverProvider),
                DriverVersion: GetStringProperty(device, DevicePropertyKey.Device_DriverVersion),
                DriverDate: GetDriverDate(device),
                Service: GetStringProperty(device, DevicePropertyKey.Device_Service),
                HardwareId: GetFirstHardwareId(device));
        }
        catch
        {
            return null;
        }
    }

    private static string? GetStringProperty(PnPDevice device, DevicePropertyKey key)
    {
        try
        {
            var value = device.GetProperty<string>(key);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetDriverDate(PnPDevice device)
    {
        try
        {
            var value = device.GetProperty<DateTimeOffset>(DevicePropertyKey.Device_DriverDate);
            return value.Year >= 1601 ? value.ToString("yyyy-MM-dd HH:mm") : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetFirstHardwareId(PnPDevice device)
    {
        try
        {
            return device.GetProperty<string[]>(DevicePropertyKey.Device_HardwareIds)
                ?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>从注册表枚举树读取显示器的原始 EDID（无 WMI 依赖，任何权限都能读）。</summary>
    private static byte[]? ReadRegistryEdid(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Enum\{instanceId.Trim()}\Device Parameters");
            return key?.GetValue("EDID") as byte[];
        }
        catch
        {
            return null;
        }
    }

    private static bool IsEdidBlock(byte[] edid)
        => edid.Length >= 128 && edid[0] == 0x00 && edid[1] == 0xFF && edid[2] == 0xFF
           && edid[3] == 0xFF && edid[4] == 0xFF && edid[5] == 0xFF && edid[6] == 0xFF && edid[7] == 0x00;

    /// <summary>解析 EDID 首选详细时序描述符（偏移 54），得到原生分辨率与刷新率。</summary>
    public static (int Width, int Height, double RefreshRate)? ParsePreferredMode(byte[]? edid)
    {
        if (edid is null || edid.Length < 72) return null;
        var pixelClockKhz = edid[54] | (edid[55] << 8);
        if (pixelClockKhz <= 0) return null;
        var hActive = edid[56] | ((edid[58] >> 4) & 0x0F) << 8;
        var hBlank = edid[57] | (edid[58] & 0x0F) << 8;
        var vActive = edid[59] | ((edid[61] >> 4) & 0x0F) << 8;
        var vBlank = edid[60] | (edid[61] & 0x0F) << 8;
        if (hActive <= 0 || vActive <= 0) return null;
        var hTotal = hActive + hBlank;
        var vTotal = vActive + vBlank;
        if (hTotal <= 0 || vTotal <= 0) return null;
        var refresh = pixelClockKhz * 10000.0 / (hTotal * vTotal);
        if (refresh <= 0 || refresh > 1000 || !double.IsFinite(refresh)) return null;
        return (hActive, vActive, refresh);
    }

    /// <summary>解码 EDID 头部的 3 字符 EISA 厂商代码（如 BOE/DEL/LGD）。</summary>
    public static string? DecodeEisaManufacturer(byte[]? edid)
    {
        if (edid is null || edid.Length < 10) return null;
        var b8 = edid[8];
        var b9 = edid[9];
        var c1 = (char)('A' - 1 + ((b8 >> 2) & 0x1F));
        var c2 = (char)('A' - 1 + (((b8 & 0x03) << 3) | ((b9 >> 5) & 0x07)));
        var c3 = (char)('A' - 1 + (b9 & 0x1F));
        if (c1 is < 'A' or > 'Z' || c2 is < 'A' or > 'Z' || c3 is < 'A' or > 'Z') return null;
        return (c1.ToString() + c2 + c3).Trim();
    }

    /// <summary>EDID 第 17 字节为出厂年份偏移（0 = 1990）。</summary>
    public static int? ParseManufactureYear(byte[]? edid)
    {
        if (edid is null || edid.Length < 18) return null;
        var year = edid[17] + 1990;
        return year is >= 1990 and <= 2100 ? year : null;
    }

    /// <summary>WmiMonitorID 的 InstanceName 形如 DISPLAY\…\UID0_0，去掉末尾的“_序号”。</summary>
    private static string NormalizeMonitorInstanceName(string? instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName)) return "";
        var s = instanceName.Trim();
        var i = s.Length;
        while (i > 0 && char.IsDigit(s[i - 1])) i--;
        return i < s.Length && i > 0 && s[i - 1] == '_' ? s[..(i - 1)] : s;
    }

    /// <summary>WmiMonitorID 的字符串字段是 UInt16[]（每元素一个 16 位字符），兼容 byte[]/char[] 形式。</summary>
    private static string? DecodeEdidString(object? value)
    {
        if (value is null) return null;
        try
        {
            var sb = new StringBuilder();
            switch (value)
            {
                case ushort[] wide:
                    foreach (var c in wide)
                    {
                        if (c == 0) break;
                        sb.Append((char)c);
                    }
                    break;
                case byte[] bytes:
                    foreach (var b in bytes)
                    {
                        if (b == 0) break;
                        sb.Append((char)b);
                    }
                    break;
                case char[] chars:
                    foreach (var c in chars)
                    {
                        if (c == '\0') break;
                        sb.Append(c);
                    }
                    break;
                case string s:
                    sb.Append(s);
                    break;
            }
            var result = sb.ToString().Trim();
            return result.Length == 0 ? null : result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>安全调用可能打开设备句柄的字符串读取（HID 厂商/序列号等）。</summary>
    private static string? GetStringSafely(Func<string?> getter)
    {
        try
        {
            var value = getter();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static void ScanKeyboards(List<PeripheralDevice> list, HashSet<string> seen)
    {
        list.AddRange(SafeQuery("root\\cimv2",
            "SELECT Name, Description, Manufacturer, Status, PNPDeviceID FROM Win32_Keyboard",
            o =>
            {
                var name = GetString(o, "Name");
                if (string.IsNullOrWhiteSpace(name)) return null;
                var mfr = GetString(o, "Manufacturer");
                var id = GetString(o, "PNPDeviceID");
                var key = "keyboard|" + name + "|" + mfr + "|" + id;
                if (!seen.Add(key)) return null;
                return new PeripheralDevice(name, mfr, GetString(o, "Description"), "keyboard", "Keyboard",
                    Detail: null, Status: GetString(o, "Status"), PnpDeviceId: id);
            }).Where(d => d is not null)!);
    }

    private static void ScanPointingDevices(List<PeripheralDevice> list, HashSet<string> seen)
    {
        list.AddRange(SafeQuery("root\\cimv2",
            "SELECT Name, Description, Manufacturer, Status, PNPDeviceID FROM Win32_PointingDevice",
            o =>
            {
                var name = GetString(o, "Name");
                if (string.IsNullOrWhiteSpace(name)) return null;
                var mfr = GetString(o, "Manufacturer");
                var id = GetString(o, "PNPDeviceID");
                var key = "mouse|" + name + "|" + mfr + "|" + id;
                if (!seen.Add(key)) return null;
                return new PeripheralDevice(name, mfr, GetString(o, "Description"), "mouse", "Mouse",
                    Detail: null, Status: GetString(o, "Status"), PnpDeviceId: id);
            }).Where(d => d is not null)!);
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
                var model = GetString(o, "Model");
                var size = GetUInt64(o, "Size");
                return new PeripheralDevice(string.IsNullOrWhiteSpace(model) ? "USB Disk" : model, Manufacturer: null, Description: "USB Disk",
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
                var id = GetString(o, "DeviceID") ?? "Removable Disk";
                var size = GetUInt64(o, "Size");
                var free = GetUInt64(o, "FreeSpace");
                var fs = GetString(o, "FileSystem");
                return new PeripheralDevice(id, Manufacturer: null, Description: "Removable Disk",
                    Category: "storage", PnpClass: "LogicalDisk",
                    Detail: $"{id}  {FormatUtil.Bytes(size is ulong s && s > 0 ? (double?)s : null, "N/A")}  free {FormatUtil.Bytes(free.HasValue ? (double?)free.Value : null, "N/A")}  {fs}");
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
                if (n.Contains("Camera", StringComparison.OrdinalIgnoreCase) || n.Contains("Webcam", StringComparison.OrdinalIgnoreCase) || n.Contains("Video", StringComparison.OrdinalIgnoreCase) || n.Contains("摄像头")) return "camera";
                if (n.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) || n.Contains("蓝牙")) return "bluetooth";
                if (n.Contains("Input", StringComparison.OrdinalIgnoreCase) || n.Contains("HID", StringComparison.OrdinalIgnoreCase)) return "input";
                if (n.Contains("Network", StringComparison.OrdinalIgnoreCase) || n.Contains("Ethernet", StringComparison.OrdinalIgnoreCase)) return "network";
                return "usb";
            case "USBHub":
                return "hub";
            case "HIDClass":
                if (n.Contains("Keyboard", StringComparison.OrdinalIgnoreCase) || n.Contains("键盘")) return "keyboard";
                if (n.Contains("Mouse", StringComparison.OrdinalIgnoreCase) || n.Contains("Pointer", StringComparison.OrdinalIgnoreCase) || n.Contains("鼠标")) return "mouse";
                if (n.Contains("Game", StringComparison.OrdinalIgnoreCase) || n.Contains("Joystick", StringComparison.OrdinalIgnoreCase) || n.Contains("手柄")) return "gamepad";
                if (n.Contains("Camera", StringComparison.OrdinalIgnoreCase) || n.Contains("摄像头")) return "camera";
                if (n.Contains("Consumer Control", StringComparison.OrdinalIgnoreCase)) return "input";
                return "input";
            default:
                if (n.Contains("摄像头", StringComparison.OrdinalIgnoreCase)
                    || ((n.Contains("Camera", StringComparison.OrdinalIgnoreCase) || n.Contains("Video", StringComparison.OrdinalIgnoreCase)) && pnpClass is "Image" or null)) return "camera";
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
        // CIM 日期为 yyyyMMddHHmmss.ffffff+zzz：保留到分钟，避免像旧实现那样只截前 8 位丢时间。
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

    private static List<T> SafeQuery<T>(string scope, string query, Func<ManagementBaseObject, T> map)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, query, WmiEnumerationOptions);
            using var results = searcher.Get();
            return results.Cast<ManagementBaseObject>().Select(map).ToList();
        }
        catch
        {
            return [];
        }
    }


    private static string? GetString(ManagementBaseObject o, string p)
    {
        try
        {
            var value = o[p];
            if (value is null || value is DBNull) return null;
            return value.ToString();
        }
        catch { return null; }
    }

    private static object? GetRaw(ManagementBaseObject o, string p)
    {
        try
        {
            var value = o[p];
            return value is DBNull ? null : value;
        }
        catch { return null; }
    }

    private static string? GetFirstString(ManagementBaseObject o, string p)
    {
        try
        {
            var value = o[p];
            if (value is null || value is DBNull) return null;
            if (value is string[] arr) return arr.FirstOrDefault(s => !string.IsNullOrEmpty(s));
            return value.ToString();
        }
        catch { return null; }
    }

    private static int? GetInt(ManagementBaseObject o, string p)
    {
        try
        {
            var value = o[p];
            if (value is null || value is DBNull) return null;
            return Convert.ToInt32(value);
        }
        catch { return null; }
    }

    private static ushort? GetUInt16(ManagementBaseObject o, string p)
    {
        try
        {
            var value = o[p];
            if (value is null || value is DBNull) return null;
            return Convert.ToUInt16(value);
        }
        catch { return null; }
    }

    private static ulong? GetUInt64(ManagementBaseObject o, string p)
    {
        try
        {
            var value = o[p];
            if (value is null || value is DBNull) return null;
            return Convert.ToUInt64(value);
        }
        catch { return null; }
    }
}
