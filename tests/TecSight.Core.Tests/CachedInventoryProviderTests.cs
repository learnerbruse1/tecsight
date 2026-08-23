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

    private sealed class NullInventoryProvider : IHardwareInventoryProvider
    {
        public string Name => "null";
        public HardwareInventory Capture() => null!;
    }
}
