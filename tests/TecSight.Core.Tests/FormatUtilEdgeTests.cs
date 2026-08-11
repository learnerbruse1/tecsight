using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class FormatUtilEdgeTests
{
    [Fact]
    public void Wh_NullOrNonFinite_ReturnsNullText()
    {
        Assert.Equal("N/A", FormatUtil.Wh(null, "N/A"));
        Assert.Equal("N/A", FormatUtil.Wh(double.NaN, "N/A"));
        Assert.Equal("N/A", FormatUtil.Wh(double.PositiveInfinity, "N/A"));
        Assert.Equal("80.0 Wh", FormatUtil.Wh(80, "N/A"));
    }

    [Fact]
    public void Gb_NullOrZero_ReturnsNullTextOrZero()
    {
        Assert.Equal("N/A", FormatUtil.Gb((long?)null, "N/A"));
        Assert.Equal("0.0 GB", FormatUtil.Gb(0L, "N/A"));
        Assert.Equal("1.0 GB", FormatUtil.Gb(1073741824L, "N/A"));
    }

    [Fact]
    public void Pct_NonFinite_ReturnsNullText()
    {
        Assert.Equal("N/A", FormatUtil.Pct(double.NaN, "N/A"));
        Assert.Equal("12.5%", FormatUtil.Pct(12.5, "N/A"));
    }

    [Fact]
    public void ExportTxt_BatteryHealthCappedAt100_WhenFullExceedsDesign()
    {
        // 满充 82.01Wh > 设计 80Wh → 健康度封顶 100%，损耗不显示负数
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory { Battery = new BatteryInfo("BAT-01", 80, 82.01, 18, "LiP", 16.5, 16.5) },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var txt = new SnapshotExporter().ExportTxt(snap);

        Assert.Contains("100.0%", txt);
        Assert.DoesNotContain("102.5%", txt);
    }

    [Fact]
    public void CompatibilityReporter_ReportsCategoriesAndMetrics()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Cpus = [new CpuInfo("Fake CPU", 4, 8, 3.2, "FakeCorp")],
                Battery = new BatteryInfo("BAT-01", 80, 82.01, 18, "LiP", 16.5, 16.5),
            },
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                CpuUsagePercent = 12.5,
                Sensors = [new SensorReading("CPU", "Temperature", 60, "°C")],
            });

        var report = CompatibilityReporter.Build(snap);

        Assert.Contains("[OK] CPU", report);
        Assert.Contains("[OK] Battery", report);
        Assert.Contains("[OK] CPU usage", report);
        Assert.Contains("Sensors: 1 total", report);
    }

    [Fact]
    public void CompatibilityReporter_NonFiniteMetric_ReportsNotDetected()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow, CpuUsagePercent = null, MemoryUsagePercent = null });

        var report = CompatibilityReporter.Build(snap);

        Assert.Contains("[--] CPU usage", report);
        Assert.Contains("[--] Memory usage", report);
    }
}
