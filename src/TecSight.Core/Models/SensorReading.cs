namespace TecSight.Core.Models;

/// <summary>传感器读数（Sensor Reading）：温度/风扇/电压等单个可测量数值。</summary>
public sealed record SensorReading(string HardwareName, string SensorName, double? Value, string Unit);