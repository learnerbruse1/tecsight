using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class CachedInventoryProviderTests
{
    private sealed class CountingProvider : IHardwareInventoryProvider
    {
        public int Calls { get; private set; }
        public string Name => "counting";
        public HardwareInventory Capture()
        {
            Calls++;
            return new HardwareInventory { ComputerName = $"PC-{Calls}" };
        }
    }

    [Fact]
    public void Capture_WithinTtl_ReturnsCachedWithoutRecallingInner()
    {
        var inner = new CountingProvider();
        var cached = new CachedInventoryProvider(inner, TimeSpan.FromMinutes(1));

        var first = cached.Capture();
        var second = cached.Capture();

        Assert.Equal("PC-1", first.ComputerName);
        Assert.Same(first, second); // 缓存返回同一实例
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public void Capture_AfterTtl_Recaptures()
    {
        var inner = new CountingProvider();
        var cached = new CachedInventoryProvider(inner, TimeSpan.FromMilliseconds(50));

        _ = cached.Capture();
        Thread.Sleep(120);
        var after = cached.Capture();

        Assert.Equal("PC-2", after.ComputerName);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public void SetTtl_AppliesToNextCapture()
    {
        var inner = new CountingProvider();
        var cached = new CachedInventoryProvider(inner, TimeSpan.FromMinutes(10));

        _ = cached.Capture();
        cached.SetTtl(TimeSpan.FromMilliseconds(50));
        Thread.Sleep(120);
        var after = cached.Capture();

        Assert.Equal("PC-2", after.ComputerName);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public void Capture_InnerReturnsNull_StoresEmptyInventory()
    {
        var inner = new NullInventoryProvider();
        var cached = new CachedInventoryProvider(inner);

        var first = cached.Capture();
        var second = cached.Capture();

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void Capture_PersistsAndLoadsCacheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tecsight-cache-{Guid.NewGuid():N}.json");
        try
        {
            var firstInner = new CountingProvider();
            var first = new CachedInventoryProvider(firstInner, TimeSpan.FromMinutes(10), path);
            _ = first.Capture();

            Assert.True(File.Exists(path));

            var secondInner = new CountingProvider();
            var second = new CachedInventoryProvider(secondInner, TimeSpan.FromMinutes(10), path);
            var loaded = second.Capture();

            Assert.Equal("PC-1", loaded.ComputerName);
            Assert.Equal(0, secondInner.Calls);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Capture_AfterTtl_WhenInnerThrows_ReturnsLastGoodCache()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tecsight-cache-throw-{Guid.NewGuid():N}.json");
        try
        {
            var seed = new CachedInventoryProvider(new CountingProvider(), TimeSpan.FromMilliseconds(50), path);
            _ = seed.Capture();

            var target = new CachedInventoryProvider(new ThrowingInventoryProvider(), TimeSpan.FromMilliseconds(1), path);
            Thread.Sleep(20);
            var result = target.Capture();

            Assert.Equal("PC-1", result.ComputerName);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Capture_LoadsCacheWithNullCollectionsAsEmptyLists()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tecsight-cache-null-lists-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
            {
              "ComputerName": "PC-NULL",
              "Cpus": [null],
              "MemoryModules": null,
              "Disks": null,
              "Gpus": null,
              "NetworkAdapters": null,
              "NetworkConfigurations": null,
              "LogicalDisks": null,
              "WifiInterfaces": null,
              "ProblemDevices": null,
              "Displays": null,
              "AudioDevices": null,
              "UsbDevices": null,
              "Keyboards": null,
              "PointingDevices": null,
              "Printers": null
            }
            """);

            var cached = new CachedInventoryProvider(new CountingProvider(), TimeSpan.FromMinutes(10), path);
            var result = cached.Capture();

            Assert.Equal("PC-NULL", result.ComputerName);
            Assert.Empty(result.Cpus);
            Assert.Empty(result.MemoryModules);
            Assert.Empty(result.Disks);
            Assert.Empty(result.NetworkAdapters);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class NullInventoryProvider : IHardwareInventoryProvider
    {
        public string Name => "null";
        public HardwareInventory Capture() => null!;
    }

    private sealed class ThrowingInventoryProvider : IHardwareInventoryProvider
    {
        public string Name => "throwing";
        public HardwareInventory Capture() => throw new InvalidOperationException("WMI unavailable");
    }
}
