using TecSight.Core;

namespace TecSight.Core.Tests;

public class MetricsCorrectnessTests
{
    [Theory]
    [InlineData(4_000_000_000.0, 8_000_000_000.0, 50.0)]
    [InlineData(0.0, 8_000_000_000.0, 0.0)]
    [InlineData(8_000_000_000.0, 8_000_000_000.0, 100.0)]
    [InlineData(12_000_000_000.0, 8_000_000_000.0, 100.0)] // 异常输入钳制到 100
    [InlineData(null, 8_000_000_000.0, null)]
    [InlineData(4_000_000_000.0, 0.0, null)]
    [InlineData(double.NaN, 8_000_000_000.0, null)]
    public void PhysicalMemoryPercent_MatchesUsedOverTotal(double? used, double? total, double? expected)
    {
        var actual = PerformanceMetricsProvider.PhysicalMemoryPercent(used, total);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ThermalZoneCelsius_ConvertsTenthsKelvinToCelsius()
    {
        Assert.Null(LibreHardwareSensorProvider.ThermalZoneCelsius(0));
        Assert.Null(LibreHardwareSensorProvider.ThermalZoneCelsius(6000));

        var around60 = LibreHardwareSensorProvider.ThermalZoneCelsius(3332);
        Assert.NotNull(around60);
        Assert.InRange(around60!.Value, 59.9, 60.1);

        var aroundZero = LibreHardwareSensorProvider.ThermalZoneCelsius(2732);
        Assert.NotNull(aroundZero);
        Assert.InRange(aroundZero!.Value, -0.1, 0.1);
    }
}
