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

    [Fact]
    public void ExportTxt_NonFiniteUptime_ShowsN_A_AndDoesNotThrow()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow, SystemUptimeSeconds = double.NaN });

        var txt = new SnapshotExporter().ExportTxt(snap);

        Assert.Contains("不可用 N/A", txt);
    }

    [Fact]
    public void ExportTxt_HugeUptime_ShowsN_A_AndDoesNotThrow()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow, SystemUptimeSeconds = double.MaxValue });

        var txt = new SnapshotExporter().ExportTxt(snap);

        Assert.Contains("不可用 N/A", txt);
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

    [Fact]
    public void ExportTxt_TruncatesOtherDeviceNamesAfterEight()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                UsbDevices = Enumerable.Range(1, 10)
                    .Select(i => new UsbDeviceInfo($"USB-{i}", null))
                    .ToList(),
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var txt = new SnapshotExporter().ExportTxt(snap);

        Assert.Contains("USB-1", txt);
        Assert.Contains("USB-8", txt);
        Assert.DoesNotContain("USB-9", txt);
        Assert.Contains("…", txt);
    }

    [Fact]
    public void ExportTxt_StorageHealthStatuses_AreLocalized()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Disks =
                [
                    new DiskInfo("GoodDisk", null, null, new StorageHealth("GoodDisk", HealthStatus.Good, null)),
                    new DiskInfo("WarnDisk", null, null, new StorageHealth("WarnDisk", HealthStatus.Warning, null)),
                    new DiskInfo("CritDisk", null, null, new StorageHealth("CritDisk", HealthStatus.Critical, null)),
                    new DiskInfo("UnknownDisk", null, null, new StorageHealth("UnknownDisk", HealthStatus.Unknown, null)),
                ],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var txt = new SnapshotExporter().ExportTxt(snap);

        Assert.Contains("良好 Good", txt);
        Assert.Contains("注意 Warning", txt);
        Assert.Contains("危险 Critical", txt);
        Assert.Contains("不可用 N/A", txt);
    }

    [Theory]
    [InlineData(0, "Off")]
    [InlineData(1, "Enabled")]
    [InlineData(2, "Running")]
    public void ExportTxt_VbsStatuses_AreLocalized(int status, string expected)
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                SystemDetails = new SystemDetails(
                    "DOMAIN", false, "TZ", true, "2.0", true, "x64",
                    VirtualizationBasedSecurityStatus: status),
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var txt = new SnapshotExporter().ExportTxt(snap);

        Assert.Contains(expected, txt);
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

    [Fact]
    public void ExportHtml_RichSnapshot_RendersHealthAndSecurityText()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Battery = new BatteryInfo("BAT-01", 80, 82.01, 18, "LiP", 16.5, 16.5),
                Disks =
                [
                    new DiskInfo("SSD", "SN", 100, new StorageHealth("SSD", HealthStatus.Good, null)),
                ],
                SystemDetails = new SystemDetails(
                    "DOMAIN", false, "China Standard Time", true, "2.0, 0, 1.38", true, "x64-based PC",
                    VirtualizationBasedSecurityStatus: 2,
                    MemoryIntegrityEnabled: true),
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("100.0%", html);
        Assert.Contains("良好", html);
        Assert.Contains("运行中", html);
        Assert.Contains("是", html);
    }

    [Fact]
    public void ExportHtml_NonFiniteUptime_ShowsN_A_AndDoesNotThrow()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                SystemUptimeSeconds = double.PositiveInfinity,
            });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("N/A", html);
        Assert.DoesNotContain("NaN", html);
        Assert.DoesNotContain("∞", html);
    }

    [Fact]
    public void ExportHtml_NegativeUptime_ShowsN_A()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                SystemUptimeSeconds = -1,
            });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("N/A", html);
    }

    [Fact]
    public void ExportHtml_EscapesHostileNames()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                ComputerName = "<script>alert('x')</script>",
                Cpus = [new CpuInfo("<b>Fake CPU</b>", 4, 8, 3.2, null)],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("&lt;b&gt;Fake CPU&lt;/b&gt;", html);
    }

    [Theory]
    [InlineData(0, "关闭")]
    [InlineData(1, "已启用（未运行）")]
    [InlineData(2, "运行中")]
    public void ExportHtml_VbsStatuses_AreLocalized(int status, string expected)
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                SystemDetails = new SystemDetails(
                    "DOMAIN", false, "TZ", true, "2.0", true, "x64",
                    VirtualizationBasedSecurityStatus: status),
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains(expected, html);
    }

    [Theory]
    [InlineData(HealthStatus.Good, "良好")]
    [InlineData(HealthStatus.Warning, "注意")]
    [InlineData(HealthStatus.Critical, "危险")]
    [InlineData(HealthStatus.Unknown, "N/A")]
    public void ExportHtml_StorageHealthStatuses_AreLocalized(HealthStatus status, string expected)
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Disks = [new DiskInfo("Disk", null, null, new StorageHealth("Disk", status, null))],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains(expected, html);
    }

    [Theory]
    [InlineData("1073741824", "1.0 GB")]
    [InlineData("0", "N/A")]
    [InlineData("not-a-number", "N/A")]
    public void ExportHtml_MemoryCapacityString_IsParsedOrN_A(string capacity, string expected)
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                MemoryModules = [new MemoryModuleInfo(capacity, "3200", "Mfr", "Part")],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains(expected, html);
    }

    [Fact]
    public void ExportHtml_BooleanFalse_ShowsNo()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                SystemDetails = new SystemDetails(
                    "DOMAIN", false, "TZ", false, null, false, "x64",
                    MemoryIntegrityEnabled: false),
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("否", html);
    }

    [Fact]
    public void ExportHtml_NonFiniteGpuEngine_ShowsN_A_NotNaN()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                GpuEngines = [new GpuEngineUsage("3D", double.NaN)],
            });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("N/A", html);
        Assert.DoesNotContain("NaN%", html);
    }

    [Fact]
    public void ExportHtml_BatteryWithoutCapacity_ShowsN_A()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Battery = new BatteryInfo("BAT-01", null, null),
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("N/A", html);
    }

    [Fact]
    public void ExportHtml_WifiSection_RendersSsidAndSignal()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                WifiInterfaces =
                [
                    new WifiInterfaceInfo("Wi-Fi", "connected", "MyNetwork", "aa:bb:cc:dd:ee:ff",
                        "802.11ax", "WPA2-Personal", 36, 90, 866, 866, "Auto Connect"),
                ],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("MyNetwork", html);
        Assert.Contains("aa:bb:cc:dd:ee:ff", html);
        Assert.Contains("90%", html);
        Assert.Contains("866 Mbps", html);
        Assert.Contains("WPA2-Personal", html);
    }

    [Fact]
    public void ExportHtml_ProblemDevicesSection_RendersErrorDetails()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                ProblemDevices =
                [
                    new ProblemDeviceInfo("Broken Device", "ROOT", "System", 22, "Disabled", "Error"),
                ],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("Broken Device", html);
        Assert.Contains("22", html);
        Assert.Contains("Disabled", html);
    }

    [Fact]
    public void ExportHtml_OtherDevicesSections_RenderNames()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Displays = [new DisplayInfo("Display-1", "Mfr", null, "SN-D1", 2024)],
                AudioDevices = [new AudioDeviceInfo("Audio-1", "Mfr", "OK")],
                UsbDevices = [new UsbDeviceInfo("USB-1", "Mfr")],
                Keyboards = [new PnPDeviceInfo("Keyboard-1", "Desc", "OK")],
                PointingDevices = [new PnPDeviceInfo("Mouse-1", "Desc", "OK")],
                Printers = [new PrinterInfo("Printer-1", "Driver", true)],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("Display-1", html);
        Assert.Contains("SN-D1", html);
        Assert.Contains("2024", html);
        Assert.Contains("Audio-1", html);
        Assert.Contains("USB-1", html);
        Assert.Contains("Keyboard-1", html);
        Assert.Contains("Mouse-1", html);
        Assert.Contains("Printer-1", html);
    }

    [Fact]
    public void ExportHtml_ProcessRow_RendersCounts()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                Processes =
                [
                    new ProcessUsage("a", 1, 1024),
                    new ProcessUsage("b", 2, 2048),
                ],
                TotalProcessCount = 10,
            });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("2 个（共 10）", html);
    }

    [Fact]
    public void ExportHtml_LogicalDisksSection_RendersVolumeDetails()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                LogicalDisks =
                [
                    new LogicalDiskInfo("C:", "System", "NTFS", 512L * 1024 * 1024 * 1024, 200L * 1024 * 1024 * 1024, 3),
                ],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("C:", html);
        Assert.Contains("System", html);
        Assert.Contains("NTFS", html);
    }

    [Fact]
    public void ExportHtml_NetworkAdapterSection_RendersDetails()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                NetworkAdapters =
                [
                    new NetworkAdapterInfo("Intel Ethernet", "AA:BB:CC:DD:EE:FF", true, 1_000_000_000,
                        "Ethernet 802.3", "Intel", DriverVersion: "1.2.3", DriverDate: "2024-01-01"),
                ],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("Intel Ethernet", html);
        Assert.Contains("AA:BB:CC:DD:EE:FF", html);
        Assert.Contains("1.0 Gbps", html);
        Assert.Contains("Intel", html);
        Assert.Contains("1.2.3", html);
        Assert.Contains("2024-01-01", html);
    }

    [Fact]
    public void ExportHtml_BiosSection_RendersDetails()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Bios = new BiosInfo("AMI", "UEFI", "1.2.3", "3.4", "2024-01-02", "SN-BIOS"),
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("AMI", html);
        Assert.Contains("UEFI", html);
        Assert.Contains("1.2.3", html);
        Assert.Contains("SN-BIOS", html);
    }

    [Fact]
    public void ExportHtml_MotherboardSection_RendersDetails()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Motherboard = new MotherboardInfo("ASUS", "TUF", "1.2.3", "2024-01-02", "ASUS", "Model"),
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("ASUS", html);
        Assert.Contains("TUF", html);
        Assert.Contains("1.2.3", html);
        Assert.Contains("Model", html);
    }

    [Fact]
    public void ExportHtml_CpuSection_RendersExtendedFields()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Cpus =
                [
                    new CpuInfo("Fake CPU", 8, 16, 3.4, "FakeCorp", "x64", "Socket",
                        L2CacheKb: 9728, L3CacheKb: 24576, CurrentClockMhz: 2400,
                        ProcessorId: "ID-1", VirtualizationFirmwareEnabled: true),
                ],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("Fake CPU", html);
        Assert.Contains("x64", html);
        Assert.Contains("Socket", html);
        Assert.Contains("9728 KB", html);
        Assert.Contains("24576 KB", html);
        Assert.Contains("2400 MHz", html);
        Assert.Contains("ID-1", html);
    }

    [Fact]
    public void ExportHtml_MemorySections_RenderDetails()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                MemoryModules =
                [
                    new MemoryModuleInfo("8589934592", "3200", "Samsung", "M471A1K43DB1",
                        MemoryType: "DDR4", ConfiguredClockMhz: "3200", ConfiguredVoltageMv: "1.2 V",
                        DeviceLocator: "DIMM 0", FormFactor: "SODIMM", Ecc: false),
                ],
                MemoryTopology = new MemoryTopologyInfo(2, 1, 64L * 1024 * 1024 * 1024, "None"),
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("Samsung", html);
        Assert.Contains("DDR4", html);
        Assert.Contains("SODIMM", html);
        Assert.Contains("2", html);
        Assert.Contains("None", html);
    }

    [Fact]
    public void ExportHtml_GpuSection_RendersDetails()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Gpus =
                [
                    new GpuInfo("NVIDIA GeForce RTX 3060", 8L * 1024 * 1024 * 1024, "32.0",
                        "2024-01-01", 1920, 1080, 60, "1920 x 1080", "NVIDIA", "GPU", "x64"),
                ],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("NVIDIA GeForce RTX 3060", html);
        Assert.Contains("32.0", html);
        Assert.Contains("8.0 GB", html);
        Assert.Contains("1920", html);
        Assert.Contains("60", html);
    }

    [Fact]
    public void ExportHtml_DiskSection_RendersSerialNumber()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Disks =
                [
                    new DiskInfo("SSD", "SERIAL-123", 512L * 1024 * 1024 * 1024, null,
                        MediaType: "SSD", BusType: "NVMe", FirmwareVersion: "FW1"),
                ],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("SERIAL-123", html);
    }

    [Fact]
    public void ExportHtml_BatterySection_RendersChemistryAndVoltage()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Battery = new BatteryInfo("BAT-01", 80, 82, 18, "LiP", 16.5, 16.5),
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("LiP", html);
        Assert.Contains("16.5", html);
    }

    [Fact]
    public void ExportHtml_SystemDetailsSection_RendersSerialAndUuid()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                SystemDetails = new SystemDetails(
                    "DOMAIN", false, "TZ", true, "2.0", true, "x64",
                    SerialNumber: "SN123",
                    Uuid: "UUID-1",
                    ProductName: "ThinkPad",
                    ProductVersion: "1.0"),
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("SN123", html);
        Assert.Contains("UUID-1", html);
        Assert.Contains("ThinkPad", html);
        Assert.Contains("1.0", html);
    }

    [Fact]
    public void ExportHtml_SummaryInstallAndBootFields_Render()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                OsCaption = "Windows 11 Pro",
                OsVersion = "10.0.26200",
                OsArchitecture = "x64",
                FirmwareType = "UEFI",
                OsInstallDate = "2024-01-01 10:00",
                LastBootTime = "2026-08-22 09:00",
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("Windows 11 Pro", html);
        Assert.Contains("10.0.26200", html);
        Assert.Contains("UEFI", html);
        Assert.Contains("2024-01-01 10:00", html);
        Assert.Contains("2026-08-22 09:00", html);
    }

    [Fact]
    public void ExportHtml_LiveMetricsRows_RenderValues()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                CpuUsagePercent = 12.5,
                MemoryUsagePercent = 40,
                GpuUsagePercent = 7,
                DiskReadBytesPerSec = 1024,
                DiskWriteBytesPerSec = 2048,
                NetworkDownloadBps = 1024,
                NetworkUploadBps = 2048,
                BatteryChargePercent = 90,
                SystemUptimeSeconds = 3661,
            });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("12.5%", html);
        Assert.Contains("40.0%", html);
        Assert.Contains("7.0%", html);
        Assert.Contains("1 KB/s", html);
        Assert.Contains("2 KB/s", html);
        Assert.Contains("90.0%", html);
        Assert.Contains("01:01", html);
    }

    [Fact]
    public void ExportHtml_SmartAttributesSection_RendersDetails()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                SmartAttributes =
                [
                    new SmartAttributeReading("Disk1", 5, "Reallocated Sectors", 100, 100, 10, "0"),
                ],
            });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("SMART 属性", html);
        Assert.Contains("Disk1", html);
        Assert.Contains("Reallocated Sectors", html);
        Assert.Contains("100", html);
        Assert.Contains("10", html);
    }

    [Fact]
    public void ExportHtml_SummaryMemoryRow_RendersUsageAndTotal()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                MemoryModules =
                [
                    new MemoryModuleInfo("8589934592", "3200", "Mfr", "Part"),
                ],
            },
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                MemoryUsagePercent = 40,
                MemoryUsedBytes = 4L * 1024 * 1024 * 1024,
                MemoryTotalBytes = 8L * 1024 * 1024 * 1024,
            });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("40.0%", html);
        Assert.Contains("4.0 GB", html);
        Assert.Contains("8.0 GB", html);
    }

    [Fact]
    public void ExportHtml_BatteryChargingState_RendersCharging()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                Battery = new BatteryInfo("BAT-01", 80, 80, 10, "LiP"),
            },
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                BatteryChargePercent = 90,
                BatteryIsCharging = true,
            });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("充电中", html);
        Assert.Contains("90.0%", html);
    }

    [Fact]
    public void ExportHtml_GpuEngineFiniteValue_RendersPercent()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                GpuEngines = [new GpuEngineUsage("3D", 12.5)],
            });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("3D", html);
        Assert.Contains("12.5%", html);
    }

    [Fact]
    public void ExportHtml_CpuFrequencyRow_RendersGhz()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                CpuUsagePercent = 10,
                CpuFrequencyMhz = 2400,
            });

        var html = new SnapshotExporter().ExportHtml(snap);

        Assert.Contains("10.0%", html);
        Assert.Contains("2400 MHz", html);
    }
}
public class SnapshotExporterNonFiniteTests
{
    [Fact]
    public void ExportJson_DoesNotThrowOnNonFiniteSensorValues()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory(),
            new LiveMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                Sensors =
                [
                    new SensorReading("HW", "NaN sensor", double.NaN, ""),
                    new SensorReading("HW", "Inf sensor", double.PositiveInfinity, ""),
                    new SensorReading("HW", "OK sensor", 42, "°C"),
                ],
            });

        var json = new SnapshotExporter().ExportJson(snap);

        Assert.Contains("OK sensor", json);
    }
}
public class SnapshotExporterEmptyTests
{
    private static Snapshot Empty() => new(
        DateTimeOffset.MinValue,
        new HardwareInventory(),
        new LiveMetrics { Timestamp = DateTimeOffset.MinValue });

    [Fact]
    public void ExportTxt_HandlesEmptySnapshot()
    {
        var txt = new SnapshotExporter().ExportTxt(Empty());

        Assert.Contains("快照", txt);
    }

    [Fact]
    public void ExportHtml_HandlesEmptySnapshot()
    {
        var html = new SnapshotExporter().ExportHtml(Empty());

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("</html>", html);
        Assert.DoesNotContain("SMART 属性", html);
    }

    [Fact]
    public void ExportJson_HandlesEmptySnapshot()
    {
        var json = new SnapshotExporter().ExportJson(Empty());

        Assert.Contains("Inventory", json);
    }
}
public class SnapshotExporterHistoryCsvTests
{
    [Fact]
    public void ExportHistoryCsv_ProducesHeaderAndRows()
    {
        var history = new[]
        {
            new LiveMetrics { Timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), CpuUsagePercent = 12.5, CpuFrequencyMhz = 2250 },
            new LiveMetrics { Timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero), CpuUsagePercent = 13.0, CpuFrequencyMhz = 2250 },
        };

        var csv = new SnapshotExporter().ExportHistoryCsv(history);

        Assert.Contains("Timestamp,CpuPercent", csv);
        Assert.Contains("2026-01-01 00:00:00,12.5,2250", csv);
        Assert.Contains("2026-01-01 00:00:01,13,2250", csv);
    }
}
