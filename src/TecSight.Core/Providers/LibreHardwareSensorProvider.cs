using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Storage;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>
/// 传感器与磁盘 SMART 真实数据源：LibreHardwareMonitor（MPL-2.0）。
/// 枚举温度/风扇/电压/负载等传感器读数；存储设备输出 SMART 健康度读数。
/// 无传感器或打开失败时返回空列表（降级）。
/// </summary>
public sealed class LibreHardwareSensorProvider : ISensorProvider, IDisposable
{
    public string Name => "librehardwaremonitor";

    private readonly Computer? _computer;

    public LibreHardwareSensorProvider()
    {
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsBatteryEnabled = true,
            IsNetworkEnabled = true,
        };
        try
        {
            computer.Open();
            _computer = computer;
        }
        catch
        {
            _computer = null;
        }
    }

    public IReadOnlyList<SensorReading> Capture()
    {
        if (_computer is null) return [];
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

        // 存储设备 SMART 健康度（0 = 需要关注，1 = 良好）
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

    private static double? SmartHealth(IHardware storage)
    {
        // 优先用 ISmart 属性判断：5 = 重映射扇区，197 = 待映射扇区，raw > 0 视为需要关注。
        if (storage is ISmart smart)
        {
            try
            {
                var attrs = smart.Attributes;
                if (attrs is { Count: > 0 })
                {
                    // 5 = 重映射扇区数，197 = 当前待映射扇区数；归一化值跌破阈值视为需要关注。
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

        // 回退：Remaining Life 传感器（百分比）
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
            _computer?.Close();
        }
        catch
        {
            // 忽略关闭异常
        }
    }
}