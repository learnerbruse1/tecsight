namespace TecSight.Core.Models;

/// <summary>硬件名称分类与主设备选择（供概览/详情/兼容性报告共用）。</summary>
public static class HardwareClassifier
{
    public static bool IsVirtualGpu(string? name) =>
        name is not null && (name.Contains("Oray", StringComparison.OrdinalIgnoreCase)
                             || name.Contains("Remote", StringComparison.OrdinalIgnoreCase)
                             || name.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                             || name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase)
                             || name.Contains("Mirror", StringComparison.OrdinalIgnoreCase));

    /// <summary>挑选主 GPU：排除虚拟显示驱动，再按显存大小取最大（不依赖溢出的 AdapterRAM 绝对值排序之外）。</summary>
    public static GpuInfo? PickPrimaryGpu(IReadOnlyList<GpuInfo> gpus) =>
        gpus.Where(g => g.Name is not null && !IsVirtualGpu(g.Name))
            .OrderByDescending(g => g.MemoryBytes ?? 0)
            .FirstOrDefault();

    public static bool MatchesCpuHw(string name) =>
        name.Contains("CPU", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Intel", StringComparison.OrdinalIgnoreCase)
        || name.Contains("AMD", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Core", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Package", StringComparison.OrdinalIgnoreCase);

    public static bool MatchesGpuHw(string name) =>
        name.Contains("GPU", StringComparison.OrdinalIgnoreCase)
        || name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)
        || name.Contains("RTX", StringComparison.OrdinalIgnoreCase)
        || name.Contains("GTX", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Graphics", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Arc", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 判断是否为虚拟/软件网络适配器（VPN、TAP、虚拟交换机、蓝牙 PAN、NDIS 过滤器等）。
    /// 用于把"物理接口"与虚拟/软件接口区分开，避免把扩展坞等真实网卡误过滤掉。
    /// </summary>
    public static bool IsVirtualNetworkAdapter(string? name, string? adapterType = null)
    {
        var text = (name ?? "") + " " + (adapterType ?? "");
        return ContainsAny(text,
            "TAP", "Tunnel", "Wintun", "Virtual", "vEthernet", "Loopback", "Remote NDIS",
            "Hyper-V", "Wi-Fi Direct", "WAN Miniport", "Kernel Debug", "Bluetooth",
            "VMware", "VirtualBox", "Hamachi", "ZeroTier", "Tailscale", "WireGuard", "UU",
            "Npcap", "NDIS", "LightWeight Filter", "WFP", "QoS Packet Scheduler", "Leigod",
            "Native WiFi Filter", "Virtual WiFi Filter");
    }

    private static bool ContainsAny(string text, params string[] markers) =>
        markers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
}
