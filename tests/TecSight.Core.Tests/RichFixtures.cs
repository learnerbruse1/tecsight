using TecSight.App;
using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

internal static class RichFixtures
{
    public static Snapshot Snapshot(DateTimeOffset? at = null) => new(
        at ?? DateTimeOffset.Now,
        new HardwareInventory
        {
            ComputerName = "FAKE-PC",
            OsCaption = "Windows 11",
            OsVersion = "10.0.26200",
            Cpus = [new CpuInfo("Fake CPU", 8, 16, 3.2, "FakeCorp")],
            MemoryModules = [new MemoryModuleInfo("8589934592", "5600", "Mfr", "Part", MemoryType: "DDR5")],
            Disks = [new DiskInfo("SSD", "SN", 512L * 1024 * 1024 * 1024, null, MediaType: "SSD", BusType: "NVMe")],
            Gpus = [new GpuInfo("Fake GPU", 8L * 1024 * 1024 * 1024, "1.0", "2025-01-01", 1920, 1080, 60)],
            Motherboard = new MotherboardInfo("Mfr", "Model", "1.0", "2025-01-01", "SysMfr", "SysModel"),
            Battery = new BatteryInfo("BAT", 80, 80, 5, "LiP"),
            NetworkAdapters =
            [
                new NetworkAdapterInfo("Ethernet", "AA:BB:CC:DD:EE:FF", true, 1_000_000_000, "Ethernet 802.3", "Intel"),
            ],
            LogicalDisks = [new LogicalDiskInfo("C:", "System", "NTFS", 512L * 1024 * 1024 * 1024, 200L * 1024 * 1024 * 1024, 3)],
        },
        new LiveMetrics
        {
            Timestamp = at ?? DateTimeOffset.Now,
            CpuUsagePercent = 12.5,
            CpuFrequencyMhz = 2400,
            MemoryUsagePercent = 50,
            MemoryUsedBytes = 4L * 1024 * 1024 * 1024,
            MemoryTotalBytes = 8L * 1024 * 1024 * 1024,
            GpuUsagePercent = 7,
            DiskReadBytesPerSec = 1024,
            DiskWriteBytesPerSec = 2048,
            NetworkDownloadBps = 1024,
            NetworkUploadBps = 2048,
            BatteryChargePercent = 90,
            SystemUptimeSeconds = 3661,
            Sensors =
            [
                new SensorReading("CPU Package", "CPU Package", 60, "°C"),
                new SensorReading("GPU", "GPU Core", 55, "°C"),
                new SensorReading("GPU", "GPU Memory Total", 6141, ""),
                new SensorReading("GPU", "GPU Memory Used", 1500, ""),
                new SensorReading("GPU", "GPU Memory Free", 4641, ""),
                new SensorReading("Motherboard", "Fan #1", 1800, "RPM"),
            ],
            SmartAttributes = [new SmartAttributeReading("SSD", 5, "Reallocated", 100, 100, 10, "0")],
            Processes = [new ProcessUsage("a", 1.5, 1024, 42), new ProcessUsage("b", 2.5, 2048, 43)],
            TotalProcessCount = 2,
            GpuEngines = [new GpuEngineUsage("3D", 12.5)],
        });

    public sealed class Collector : ISnapshotCollector
    {
        public Snapshot Collect() => Snapshot();
    }

    public static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            throw new Xunit.Sdk.XunitException(error.ToString());
        }
    }
}
