using System.Text.Json;
using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class SnapshotExporterTests
{
    internal static Snapshot MakeSnapshot() => new(
        new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        new HardwareInventory
        {
            ComputerName = "FAKE-PC",
            Cpus = [new CpuInfo("Fake CPU", 4, 8, 3.2, "FakeCorp")],
        },
        new LiveMetrics
        {
            Timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
            CpuUsagePercent = 12.5,
            Sensors = [new SensorReading("CPU", "Temperature", 60, "°C")],
        });

    [Fact]
    public void ExportJson_ProducesValidJsonWithKeyValues()
    {
        var exporter = new SnapshotExporter();
        var json = exporter.ExportJson(MakeSnapshot());

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("FAKE-PC", root.GetProperty("Inventory").GetProperty("ComputerName").GetString());
        Assert.Equal(12.5, root.GetProperty("Metrics").GetProperty("CpuUsagePercent").GetDouble());
        Assert.Equal("Temperature", root.GetProperty("Metrics").GetProperty("Sensors")[0].GetProperty("SensorName").GetString());
    }

    [Fact]
    public void ExportTxt_ContainsKeyFields()
    {
        var exporter = new SnapshotExporter();
        var txt = exporter.ExportTxt(MakeSnapshot());

        Assert.Contains("FAKE-PC", txt);
        Assert.Contains("Fake CPU", txt);
        Assert.Contains("12.5", txt);
        Assert.Contains("Temperature", txt);
    }
}
public class SnapshotExporterDetailsTests
{
    private static Snapshot MakeRichSnapshot() => new(
        DateTimeOffset.UtcNow,
        new HardwareInventory
        {
            Battery = new BatteryInfo("BAT-01", 80, 82.01, 18, "LiP", 16.5, 16.5),
            Displays = [new DisplayInfo("内置屏", "BOE", null)],
            UsbDevices = [new UsbDeviceInfo("USB Device", "Mfr")],
            Printers = [new PrinterInfo("PDF", "Microsoft Print to PDF", true)],
        },
        new LiveMetrics
        {
            Timestamp = DateTimeOffset.UtcNow,
            Sensors = Enumerable.Range(0, 100).Select(i => new SensorReading("HW", $"Sensor{i}", i, "°C")).ToList(),
        });

    [Fact]
    public void ExportTxt_TruncatesLongSensorList()
    {
        var txt = new SnapshotExporter().ExportTxt(MakeRichSnapshot());

        Assert.Contains("Sensor0", txt);
        Assert.Contains("其余 40 条", txt);
        Assert.DoesNotContain("Sensor99", txt);
    }

    [Fact]
    public void ExportTxt_ContainsBatteryAndOtherDevices()
    {
        var txt = new SnapshotExporter().ExportTxt(MakeRichSnapshot());

        Assert.Contains("BAT-01", txt);
        Assert.Contains("LiP", txt);
        Assert.Contains("BOE", txt);
        Assert.Contains("USB Device", txt);
        Assert.Contains("PDF", txt);
    }
}
public class SnapshotExporterHtmlTests
{
    [Fact]
    public void ExportHtml_ProducesSelfContainedReportWithKeyValues()
    {
        var exporter = new SnapshotExporter();
        var html = exporter.ExportHtml(SnapshotExporterTests.MakeSnapshot());

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("FAKE-PC", html);
        Assert.Contains("Fake CPU", html);
        Assert.Contains("12.5", html);
        Assert.Contains("</html>", html);
    }
}