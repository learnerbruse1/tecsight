using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class HardwareClassifierTests
{
    [Fact]
    public void PickPrimaryGpu_ExcludesVirtualAndPicksLargest()
    {
        var gpus = new List<GpuInfo>
        {
            new("OrayIddDriver Device", 0, "1.0"),
            new("Microsoft Basic Display Adapter", 0, "1.0"),
            new("NVIDIA GeForce RTX 3060", 8L * 1024 * 1024 * 1024, "32.0"),
            new("Intel(R) UHD Graphics", 1L * 1024 * 1024 * 1024, "31.0"),
        };

        var primary = HardwareClassifier.PickPrimaryGpu(gpus);

        Assert.NotNull(primary);
        Assert.Equal("NVIDIA GeForce RTX 3060", primary!.Name);
    }

    [Fact]
    public void PickPrimaryGpu_EmptyOrNull_ReturnsNull()
    {
        Assert.Null(HardwareClassifier.PickPrimaryGpu([]));
    }

    [Theory]
    [InlineData("Intel Core i7", true)]
    [InlineData("AMD Ryzen 7", true)]
    [InlineData("AMD Ryzen 7 5800H with Radeon Graphics", true)]
    [InlineData("CPU Package", true)]
    [InlineData("Intel(R) Ethernet Controller I225-V", false)]
    [InlineData("NVIDIA GeForce RTX 4050", false)]
    [InlineData("AMD Radeon RX 7900 XTX", false)]
    [InlineData("Intel(R) Arc(TM) A770 Graphics", false)]
    [InlineData("", false)]
    public void MatchesCpuHw_DetectsCpuHardware(string name, bool expected)
    {
        Assert.Equal(expected, HardwareClassifier.MatchesCpuHw(name));
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4050 Laptop GPU", true)]
    [InlineData("Intel(R) UHD Graphics", true)]
    [InlineData("AMD Radeon", true)]
    [InlineData("13th Gen Intel Core i7", false)]
    [InlineData("", false)]
    public void MatchesGpuHw_DetectsGpuHardware(string name, bool expected)
    {
        Assert.Equal(expected, HardwareClassifier.MatchesGpuHw(name));
    }

    [Fact]
    public void PickPrimaryGpu_WhenAllVirtual_ReturnsNull()
    {
        // 只有虚拟显卡（如远程桌面）时返回 null → 界面如实显示 N/A
        var gpus = new List<GpuInfo> { new("OrayIddDriver Device", 0, "1.0") };

        Assert.Null(HardwareClassifier.PickPrimaryGpu(gpus));
    }

    [Theory]
    [InlineData("Remote Desktop Adapter", true)]
    [InlineData("Virtual Display Adapter", true)]
    [InlineData("Mirror Driver", true)]
    [InlineData("Microsoft Basic Display Adapter", true)]
    [InlineData("NVIDIA GeForce RTX 3060", false)]
    public void IsVirtualGpu_DetectsVirtualDisplayDrivers(string name, bool expected)
    {
        Assert.Equal(expected, HardwareClassifier.IsVirtualGpu(name));
    }

    [Theory]
    [InlineData("Realtek USB GbE Family Controller", false)]
    [InlineData("ASIX AX88179 USB 3.0 to Gigabit Ethernet Adapter", false)]
    [InlineData("Intel(R) Ethernet Controller I225-V", false)]
    [InlineData("TAP-Windows Adapter V9", true)]
    [InlineData("vEthernet (Default Switch)", true)]
    [InlineData("Bluetooth Device (Personal Area Network)", true)]
    [InlineData("Microsoft Wi-Fi Direct Virtual Adapter", true)]
    [InlineData("WAN Miniport (IP)", true)]
    [InlineData("Tailscale", true)]
    public void IsVirtualNetworkAdapter_ClassifiesDocksAndVirtualAdapters(string name, bool expected)
    {
        Assert.Equal(expected, HardwareClassifier.IsVirtualNetworkAdapter(name));
    }
}
