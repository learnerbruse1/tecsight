using System.Diagnostics;
using System.Runtime.InteropServices;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>
/// 运行指标真实数据源：性能计数器 + 原生 API，免管理员权限。
/// CPU/内存/磁盘 I/O/网络/GPU 占用/电池/进程排行/GPU 引擎/运行时长；
/// 任一计数器不可用时该指标降级为 null / 空。
/// </summary>
public sealed class PerformanceMetricsProvider : ILiveMetricsProvider, IDisposable
{
    public string Name => "performance-counters";

    private const byte BatteryPercentUnknown = 255;
    private const byte BatteryFlagCharging = 0x08;
    private const string EngineTypePrefix = "engtype_";

    private readonly PerformanceCounter? _cpu;
    private readonly PerformanceCounter? _cpuFreq;
    private readonly PerformanceCounter? _memoryPercent;
    private readonly PerformanceCounter? _diskRead;
    private readonly PerformanceCounter? _diskWrite;
    private List<PerformanceCounter> _networkDown = [];
    private List<PerformanceCounter> _networkUp = [];
    private List<PerformanceCounter> _gpuEngines = [];
    private DateTimeOffset _lastNetworkRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset _lastGpuRefresh = DateTimeOffset.MinValue;
    private readonly Dictionary<int, (string Name, TimeSpan Cpu)> _prevProcessCpu = new();
    private DateTimeOffset _prevProcessTime = DateTimeOffset.MinValue;

    public PerformanceMetricsProvider()
    {
        _cpu = CreateCounter("Processor Information", "% Processor Utility", "_Total")
               ?? CreateCounter("Processor", "% Processor Time", "_Total");
        _cpuFreq = CreateCounter("Processor Information", "Processor Frequency", "_Total");
        _memoryPercent = CreateCounter("Memory", "% Committed Bytes In Use", null);
        _diskRead = CreateCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
        _diskWrite = CreateCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
    }

    public LiveMetrics Capture()
    {
        var ts = DateTimeOffset.Now;
        var (total, used) = MemoryBytes();
        var (batteryPercent, batteryCharging) = BatteryStatus();
        var (engines, gpuPercent) = ReadGpuUsage();
        var processes = CaptureProcesses();
        return new LiveMetrics
        {
            Timestamp = ts,
            CpuUsagePercent = Clamp01(ReadCounter(_cpu)),
            CpuFrequencyMhz = ReadCounter(_cpuFreq),
            MemoryUsagePercent = Clamp01(ReadCounter(_memoryPercent)),
            MemoryUsedBytes = used,
            MemoryTotalBytes = total,
            DiskReadBytesPerSec = ReadCounter(_diskRead),
            DiskWriteBytesPerSec = ReadCounter(_diskWrite),
            GpuUsagePercent = gpuPercent,
            NetworkDownloadBps = ReadNetworkDown(),
            NetworkUploadBps = ReadNetworkUp(),
            BatteryChargePercent = batteryPercent,
            BatteryIsCharging = batteryCharging,
            SystemUptimeSeconds = ReadUptimeSeconds(),
            Processes = processes.Top,
            TotalProcessCount = processes.Total,
            GpuEngines = engines,
        };
    }

    /// <summary>GPU 占用：按引擎类型分组求和，取最繁忙的引擎类型（通常 3D），并返回各引擎明细。</summary>
    private (IReadOnlyList<GpuEngineUsage> Engines, double? Percent) ReadGpuUsage()
    {
        try
        {
            RefreshGpuCountersIfStale();
            if (_gpuEngines.Count == 0) return ([], null); // 无计数器 → 不可用，不造假
            var sums = new Dictionary<string, double>();
            foreach (var c in _gpuEngines)
            {
                var v = c.NextValue();
                if (v <= 0) continue;
                var type = ExtractEngineType(c.InstanceName);
                sums[type] = sums.GetValueOrDefault(type) + v;
            }
            var engines = sums.Select(kv => new GpuEngineUsage(kv.Key, Math.Clamp(kv.Value, 0, 100))).ToList();
            var max = engines.Count == 0 ? 0 : engines.Max(e => e.Percent);
            return (engines, Clamp01(max));
        }
        catch
        {
            return ([], null);
        }
    }

    private static string ExtractEngineType(string instance)
    {
        var idx = instance.LastIndexOf(EngineTypePrefix, StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? instance[(idx + EngineTypePrefix.Length)..] : instance;
    }

    /// <summary>进程占用排行：按 CPU 增量排序取前 20（首帧 CPU 为 null，内存始终可用）。</summary>
    private (IReadOnlyList<ProcessUsage> Top, int Total) CaptureProcesses()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var dt = (now - _prevProcessTime).TotalSeconds;
            var all = Process.GetProcesses();
            var list = new List<ProcessUsage>(all.Length);
            foreach (var p in all)
            {
                try
                {
                    var name = string.IsNullOrEmpty(p.ProcessName) ? $"PID {p.Id}" : p.ProcessName;
                    var mem = p.WorkingSet64;
                    double? cpuPct = null;
                    if (_prevProcessCpu.TryGetValue(p.Id, out var prev) && dt > 0)
                    {
                        var delta = (p.TotalProcessorTime - prev.Cpu).TotalSeconds;
                        cpuPct = Math.Clamp(delta / dt / Environment.ProcessorCount * 100, 0, 100);
                    }
                    _prevProcessCpu[p.Id] = (name, p.TotalProcessorTime);
                    list.Add(new ProcessUsage(name, cpuPct, mem, p.Id));
                }
                catch
                {
                    // 其他用户进程访问受限等，跳过（降级）
                }
                finally
                {
                    p.Dispose();
                }
            }
            _prevProcessTime = now;
            if (_prevProcessCpu.Count > 2000) _prevProcessCpu.Clear();
            var top = list.OrderByDescending(x => x.CpuPercent ?? -1).Take(20).ToList();
            return (top, all.Length);
        }
        catch
        {
            return ([], 0);
        }
    }

    private double? ReadUptimeSeconds()
    {
        try
        {
            return GetTickCount64() / 1000.0;
        }
        catch
        {
            return null;
        }
    }

    private double? ReadNetworkDown()
    {
        try
        {
            RefreshNetworkCountersIfStale();
            if (_networkDown.Count == 0) return null; // 无计数器 → 不可用，不造假
            var sum = 0.0;
            foreach (var c in _networkDown)
            {
                var v = c.NextValue();
                if (v > 0) sum += v;
            }
            return sum;
        }
        catch
        {
            return null;
        }
    }

    private double? ReadNetworkUp()
    {
        try
        {
            RefreshNetworkCountersIfStale();
            if (_networkUp.Count == 0) return null;
            var sum = 0.0;
            foreach (var c in _networkUp)
            {
                var v = c.NextValue();
                if (v > 0) sum += v;
            }
            return sum;
        }
        catch
        {
            return null;
        }
    }

    private void RefreshNetworkCountersIfStale()
    {
        if (DateTimeOffset.UtcNow - _lastNetworkRefresh < TimeSpan.FromSeconds(30)) return;
        _lastNetworkRefresh = DateTimeOffset.UtcNow;
        DisposeAll(_networkDown); DisposeAll(_networkUp);
        _networkDown = [];
        _networkUp = [];
        try
        {
            var category = new PerformanceCounterCategory("Network Interface");
            foreach (var instance in category.GetInstanceNames())
            {
                _networkDown.Add(new PerformanceCounter("Network Interface", "Bytes Received/sec", instance));
                _networkUp.Add(new PerformanceCounter("Network Interface", "Bytes Sent/sec", instance));
            }
        }
        catch
        {
            // 网络计数器不可用时保持空列表
        }
    }

    private void RefreshGpuCountersIfStale()
    {
        if (DateTimeOffset.UtcNow - _lastGpuRefresh < TimeSpan.FromSeconds(30)) return;
        _lastGpuRefresh = DateTimeOffset.UtcNow;
        DisposeAll(_gpuEngines);
        _gpuEngines = [];
        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            foreach (var instance in category.GetInstanceNames())
            {
                try
                {
                    _gpuEngines.Add(new PerformanceCounter("GPU Engine", "Utilization Percentage", instance));
                }
                catch
                {
                    // 单个实例不可用时忽略
                }
            }
        }
        catch
        {
            // GPU Engine 计数器不可用时保持空列表
        }
    }

    private static double? ReadCounter(PerformanceCounter? counter)
    {
        try
        {
            if (counter is null) return null;
            var v = counter.NextValue();
            return v < 0 ? 0 : v;
        }
        catch
        {
            return null;
        }
    }

    private static PerformanceCounter? CreateCounter(string category, string counter, string? instance)
    {
        try
        {
            return instance is null
                ? new PerformanceCounter(category, counter, readOnly: true)
                : new PerformanceCounter(category, counter, instance, readOnly: true);
        }
        catch
        {
            return null;
        }
    }

    private static double? Clamp01(double? v)
    {
        if (v is null) return null;
        return Math.Clamp(v.Value, 0, 100);
    }

    private static void DisposeAll(IEnumerable<PerformanceCounter> counters)
    {
        foreach (var c in counters)
        {
            try { c.Dispose(); } catch { }
        }
    }

    public void Dispose()
    {
        _cpu?.Dispose();
        _cpuFreq?.Dispose();
        _memoryPercent?.Dispose();
        _diskRead?.Dispose();
        _diskWrite?.Dispose();
        DisposeAll(_networkDown);
        DisposeAll(_networkUp);
        DisposeAll(_gpuEngines);
    }

    // ---- 原生 API ----
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus sps);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AclLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    private static (double? Total, double? Used) MemoryBytes()
    {
        try
        {
            var m = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref m) || m.ullTotalPhys == 0) return (null, null);
            var used = (double)(m.ullTotalPhys - m.ullAvailPhys);
            return (m.ullTotalPhys, used);
        }
        catch
        {
            return (null, null);
        }
    }

    private static (double? Percent, bool? Charging) BatteryStatus()
    {
        try
        {
            if (!GetSystemPowerStatus(out var sps)) return (null, null);
            double? percent = sps.BatteryLifePercent == BatteryPercentUnknown ? null : sps.BatteryLifePercent;
            bool? charging;
            if (sps.AclLineStatus == 0)
            {
                charging = false; // 用电池
            }
            else if (sps.BatteryFlag == BatteryPercentUnknown)
            {
                charging = null; // 接电源但状态未知
            }
            else
            {
                charging = (sps.BatteryFlag & BatteryFlagCharging) != 0; // 接电源：8 = 充电中
            }
            return (percent, charging);
        }
        catch
        {
            return (null, null);
        }
    }
}