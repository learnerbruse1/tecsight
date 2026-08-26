using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Storage;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>SMART 属性数据源（F7）：磁盘 SMART 属性表。</summary>
public interface ISmartProvider
{
    IReadOnlyList<SmartAttributeReading> CaptureSmart();
}

/// <summary>
/// 传感器与磁盘 SMART 真实数据源：LibreHardwareMonitor（MPL-2.0）。
/// 枚举温度/风扇/电压/负载等传感器读数；存储设备输出 SMART 健康度与属性表。
/// 无传感器或打开失败时返回空列表（降级）。
/// </summary>
public sealed class LibreHardwareSensorProvider : ISensorProvider, ISmartProvider, IDisposable
{
    public string Name => "librehardwaremonitor";

    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMotherboardEnabled = true,
        IsStorageEnabled = true,
        IsBatteryEnabled = true,
        IsNetworkEnabled = true,
    };
    private readonly object _gate = new();
    private bool _opened;
    private DateTimeOffset _lastOpenAttemptUtc = DateTimeOffset.MinValue;
    private IReadOnlyList<SensorReading>? _cachedSensors;
    private DateTimeOffset _lastSensorsUtc = DateTimeOffset.MinValue;
    private IReadOnlyList<SmartAttributeReading>? _cachedSmart;
    private DateTimeOffset _lastSmartUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastThermalFallbackUtc = DateTimeOffset.MinValue;
    private const double SensorIntervalSeconds = 2.0; // 温度/风扇变化慢，2 秒更新一次足够
    private const double SmartIntervalSeconds = 5.0;  // SMART 属性基本不变，5 秒足够

    /// <summary>构造时不扫描硬件；Open()（可能较慢）延迟到首次采集，运行在后台线程，避免阻塞 UI 启动。</summary>
    public LibreHardwareSensorProvider() { }

    private void EnsureOpened()
    {
        if (_opened) return;
        lock (_gate)
        {
            if (_opened) return;
            // 打开失败不永久放弃：30 秒后重试（例如权限提升或驱动加载临时失败后可恢复）。
            if (DateTimeOffset.UtcNow - _lastOpenAttemptUtc < TimeSpan.FromSeconds(30)) return;
            _lastOpenAttemptUtc = DateTimeOffset.UtcNow;
            try
            {
                _computer.Open();
                _opened = true;
            }
            catch
            {
                // 保持关闭，下一次采集周期再重试
            }
        }
    }

    public IReadOnlyList<SensorReading> Capture()
    {
        EnsureOpened();
        if (!_opened) return [];
        lock (_gate)
        {
            if (_cachedSensors is not null && DateTimeOffset.UtcNow - _lastSensorsUtc < TimeSpan.FromSeconds(SensorIntervalSeconds))
            {
                return _cachedSensors;
            }
            var result = new List<SensorReading>();
            try
            {
                foreach (var hardware in _computer.Hardware)
                {
                    Visit(hardware, result);
                }
            }
            catch
            {
                // 采集过程中异常时返回已收集部分（降级）
            }
            // LibreHardwareMonitor 在一些新机型上读不到 CPU 温度/风扇（EC/SuperIO 未暴露）。
            // 此时用 Windows 自带 ACPI 热区兜底，覆盖能暴露热区的机型；两者都没有则保持不可用。
            var hasCpuTemp = result.Any(s => s.Unit == "°C" && HardwareClassifier.MatchesCpuHw(s.HardwareName));
            if (!hasCpuTemp && DateTimeOffset.UtcNow - _lastThermalFallbackUtc >= TimeSpan.FromSeconds(60))
            {
                // 每 60 秒最多探测一次，避免在既不支持热区设备也不支持 WMI 热区的机器上频繁超时。
                _lastThermalFallbackUtc = DateTimeOffset.UtcNow;
                AppendThermalZoneFallback(result);
            }
            _cachedSensors = result;
            _lastSensorsUtc = DateTimeOffset.UtcNow;
            return result;
        }
    }

    public IReadOnlyList<SmartAttributeReading> CaptureSmart()
    {
        EnsureOpened();
        if (!_opened) return [];
        lock (_gate)
        {
            if (_cachedSmart is not null && DateTimeOffset.UtcNow - _lastSmartUtc < TimeSpan.FromSeconds(SmartIntervalSeconds))
            {
                return _cachedSmart;
            }
            var result = new List<SmartAttributeReading>();
            try
            {
                foreach (var hardware in _computer.Hardware)
                {
                    VisitSmart(hardware, result);
                }
            }
            catch
            {
                // 返回已收集部分（降级）
            }
            _cachedSmart = result;
            _lastSmartUtc = DateTimeOffset.UtcNow;
            return result;
        }
    }

    private static void Visit(IHardware hardware, List<SensorReading> result)
    {
        try
        {
            hardware.Update();
        }
        catch
        {
            return;
        }

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is float value && float.IsFinite(value) && IsWantedType(sensor.SensorType))
            {
                result.Add(new SensorReading(hardware.Name, sensor.Name, value, UnitFor(sensor.SensorType)));
            }
        }

        if (hardware.HardwareType == HardwareType.Storage)
        {
            var health = SmartHealth(hardware);
            if (health.HasValue)
            {
                result.Add(new SensorReading(hardware.Name, "SMART Health", health.Value, ""));
            }
        }

        foreach (var sub in hardware.SubHardware)
        {
            Visit(sub, result);
        }
    }

    /// <summary>ACPI 热区温度兜底：\\.\ThermalZone（IOCTL_THERMAL_READ_TEMPERATURE）+ WMI MSAcpi_ThermalZoneTemperature。</summary>
    private static void AppendThermalZoneFallback(List<SensorReading> result)
    {
        // 1) Windows 热管理器设备（无需管理员）。
        try
        {
            using var handle = CreateFile(@"\\.\ThermalZone", 0x80000000u, 3, IntPtr.Zero, 3, 0x80, IntPtr.Zero);
            if (!handle.IsInvalid)
            {
                var input = new byte[8];
                var output = new byte[4];
                if (DeviceIoControl(handle, 0x294090, input, 8, output, 4, out _, IntPtr.Zero))
                {
                    var tenthsKelvin = BitConverter.ToUInt32(output, 0);
                    var celsius = ThermalZoneCelsius(tenthsKelvin);
                    if (celsius.HasValue)
                    {
                        result.Add(new SensorReading("CPU Thermal Zone", "ACPI Thermal Zone",
                            celsius.Value, "°C"));
                    }
                }
            }
        }
        catch
        {
            // 设备不存在或不可读时忽略
        }

        // 2) WMI 热区（许多笔记本无需管理员）。
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI",
                "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature WHERE Active=TRUE",
                new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(5) });
            using var results = searcher.Get();
            foreach (ManagementBaseObject o in results)
            {
                uint tenthsKelvin;
                try
                {
                    tenthsKelvin = Convert.ToUInt32(o["CurrentTemperature"]);
                }
                catch
                {
                    continue;
                }
                var celsius = ThermalZoneCelsius(tenthsKelvin);
                if (!celsius.HasValue) continue;
                var instance = o["InstanceName"]?.ToString() ?? "";
                var zone = instance[(instance.LastIndexOf('\\') + 1)..];
                result.Add(new SensorReading("CPU Thermal Zone", $"Thermal Zone {zone}",
                    celsius.Value, "°C"));
            }
        }
        catch
        {
            // 类不存在或拒绝访问时忽略
        }
    }

    /// <summary>把 ACPI 热区温度（十分之一开尔文）转成摄氏度，异常值返回 null。</summary>
    internal static double? ThermalZoneCelsius(uint tenthsKelvin)
    {
        if (tenthsKelvin is 0 or > 5000) return null; // 约 -273.15..226.85°C 的合理区间
        return Math.Round(tenthsKelvin / 10.0 - 273.15, 1);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode, byte[]? lpInBuffer, uint nInBufferSize,
        byte[]? lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

    private static void VisitSmart(IHardware hardware, List<SmartAttributeReading> result)
    {
        try
        {
            hardware.Update();
        }
        catch
        {
            return;
        }

        if (hardware.HardwareType == HardwareType.Storage && hardware is ISmart smart)
        {
            try
            {
                foreach (var attr in smart.Attributes)
                {
                    if (attr.IsHiddenByDefault) continue;
                    result.Add(new SmartAttributeReading(
                        hardware.Name,
                        attr.Id,
                        attr.Name,
                        attr.Value,
                        attr.Attribute?.Attribute.WorstValue,
                        attr.Threshold,
                        RawToString(attr.Attribute?.Attribute.RawValue)));
                }
            }
            catch
            {
                // 单个磁盘属性读取失败时跳过
            }
        }

        foreach (var sub in hardware.SubHardware)
        {
            VisitSmart(sub, result);
        }
    }

    private static string RawToString(byte[]? raw)
    {
        if (raw is null || raw.Length == 0) return "";
        // 小端合成十进制（CrystalDiskInfo 风格）
        ulong v = 0;
        for (var i = 0; i < Math.Min(raw.Length, 8); i++)
        {
            v |= (ulong)raw[i] << (8 * i);
        }
        return v.ToString();
    }

    private static double? SmartHealth(IHardware storage)
    {
        if (storage is ISmart smart)
        {
            try
            {
                var attrs = smart.Attributes;
                if (attrs is { Count: > 0 })
                {
                    var concerning = attrs.Any(a =>
                        (a.Id is 5 or 197) && a.Value > 0 && a.Threshold > 0 && a.Value <= a.Threshold);
                    return concerning ? 0 : 1;
                }
            }
            catch
            {
                // 属性读取失败时回退到 Remaining Life
            }
        }

        try
        {
            var remaining = storage.Sensors
                .FirstOrDefault(s => s.Name.Contains("Remaining Life", StringComparison.OrdinalIgnoreCase));
            if (remaining?.Value is float rl)
            {
                return rl > 20 ? 1 : 0;
            }
        }
        catch
        {
            // 忽略
        }

        return null;
    }

    private static bool IsWantedType(SensorType type) => type is
        SensorType.Temperature or SensorType.Fan or SensorType.Voltage or SensorType.Load
        or SensorType.Power or SensorType.Clock or SensorType.Flow or SensorType.Level
        or SensorType.Data or SensorType.SmallData;

    private static string UnitFor(SensorType type) => type switch
    {
        SensorType.Temperature => "°C",
        SensorType.Fan => "RPM",
        SensorType.Voltage => "V",
        SensorType.Load or SensorType.Level => "%",
        SensorType.Power => "W",
        SensorType.Clock => "MHz",
        SensorType.Flow => "L/h",
        _ => "",
    };

    public void Dispose()
    {
        lock (_gate)
        {
            try
            {
                if (_opened)
                {
                    _computer.Close();
                }
            }
            catch
            {
                // 忽略关闭异常
            }
            _opened = false;
        }
    }
}
