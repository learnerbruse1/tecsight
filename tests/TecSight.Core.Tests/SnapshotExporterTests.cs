using System.Text.Json;
using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class SnapshotExporterTests
{
    private static Snapshot MakeSnapshot() => new(
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