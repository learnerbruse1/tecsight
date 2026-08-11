using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Storage;
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
    private bool _openFailed;

    /// <summary>构造时不扫描硬件；Open()（可能较慢）延迟到首次采集，运行在后台线程，避免阻塞 UI 启动。</summary>
    public LibreHardwareSensorProvider() { }

    private void EnsureOpened()
    {
        if (_opened || _openFailed) return;
        lock (_gate)
        {
            if (_opened || _openFailed) return;
            try
            {
                _computer.Open();
                _opened = true;
            }
            catch
            {
                _openFailed = true;
            }
        }
    }

    public IReadOnlyList<SensorReading> Capture()
    {
        EnsureOpened();
        if (!_opened) return [];
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
        return result;
    }

    public IReadOnlyList<SmartAttributeReading> CaptureSmart()
    {
        EnsureOpened();
        if (!_opened) return [];
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
        return result;
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
            if (sensor.Value is float value && IsWantedType(sensor.SensorType))
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
    }
}