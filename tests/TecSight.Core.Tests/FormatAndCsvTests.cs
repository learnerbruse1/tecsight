using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class FormatBytesBoundaryTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1073741824, "1 GB")]
    [InlineData(1099511627776, "1 TB")]
    public void Bytes_UnitBoundaries(double b, string expected)
    {
        Assert.Equal(expected, FormatUtil.Bytes(b, "N/A"));
    }

    [Fact]
    public void Bytes_Null_ReturnsNullText()
    {
        Assert.Equal("N/A", FormatUtil.Bytes(null, "N/A"));
    }
}

public class ExportHistoryCsvNullTests
{
    [Fact]
    public void ExportHistoryCsv_NullValuesProduceEmptyCells()
    {
        var history = new[]
        {
            new LiveMetrics { Timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), CpuUsagePercent = null, GpuUsagePercent = null },
        };

        var csv = new SnapshotExporter().ExportHistoryCsv(history);

        // 头部 + 一行；null 数值输出为空单元格
        var lines = csv.TrimEnd().Split('\n');
        Assert.Equal(2, lines.Length);
        var row = lines[1];
        var cells = row.Split(',');
        Assert.Equal("2026-01-01 00:00:00", cells[0]);
        Assert.Equal("", cells[1]); // CpuPercent null
        Assert.Equal("", cells[6]); // GpuPercent null
    }

    [Fact]
    public void ExportHistoryCsv_EmptyHistory_OnlyHeader()
    {
        var csv = new SnapshotExporter().ExportHistoryCsv([]);

        Assert.Equal(1, csv.TrimEnd().Split('\n').Length);
        Assert.StartsWith("Timestamp,CpuPercent", csv);
    }
}
