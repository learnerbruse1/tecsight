using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class ExportCacheNullTests
{
    private static Snapshot MakeCpuWithoutCache() => new(
        DateTimeOffset.UtcNow,
        new HardwareInventory { Cpus = [new CpuInfo("Fake CPU", 4, 8, 3.2, "FakeCorp", L2CacheKb: null, L3CacheKb: null, CurrentClockMhz: null)] },
        new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

    [Fact]
    public void ExportTxt_NullCache_ShowsN_A_NotZero()
    {
        var txt = new SnapshotExporter().ExportTxt(MakeCpuWithoutCache());

        Assert.DoesNotContain("L2=0KB", txt);
        Assert.DoesNotContain("L3=0KB", txt);
        Assert.Contains("L2=N/A", txt);
        Assert.Contains("L3=N/A", txt);
    }

    [Fact]
    public void ExportHtml_NullCache_ShowsN_A_NotZero()
    {
        var html = new SnapshotExporter().ExportHtml(MakeCpuWithoutCache());

        Assert.DoesNotContain("0 KB", html);
        Assert.DoesNotContain("0 MHz", html);
        Assert.Contains("N/A", html);
    }

    [Fact]
    public void ExportTxt_HasCache_ShowsValues()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory { Cpus = [new CpuInfo("Fake CPU", 4, 8, 3.2, "FakeCorp", L2CacheKb: 9728, L3CacheKb: 24576, CurrentClockMhz: 2400)] },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var txt = new SnapshotExporter().ExportTxt(snap);

        Assert.Contains("L2=9728KB", txt);
        Assert.Contains("L3=24576KB", txt);
    }
}
