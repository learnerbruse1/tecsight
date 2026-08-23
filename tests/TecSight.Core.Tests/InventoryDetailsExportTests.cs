using System.Text.Json;
using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class InventoryDetailsExportTests
{
    private static Snapshot MakeSnapshot() => new(
        DateTimeOffset.UtcNow,
        new HardwareInventory
        {
            ComputerName = "FAKE-PC",
            LogicalDisks = [new LogicalDiskInfo("C:", "系统", "NTFS", 512L * 1024 * 1024 * 1024, 200L * 1024 * 1024 * 1024, 3)],
            MemoryTopology = new MemoryTopologyInfo(4, 2, 64L * 1024 * 1024 * 1024, "None"),
            SystemDetails = new SystemDetails("WORKGROUP", false, "中国标准时间", true, "2.0, 0, 1.38", true, "x64-based PC",
                SerialNumber: "SN123",
                Uuid: "uuid-1",
                ProductName: "ThinkPad",
                ProductVersion: "1.0",
                VirtualizationBasedSecurityStatus: 2,
                MemoryIntegrityEnabled: true,
                CodeIntegrityStatus: 2),
            ProblemDevices = [new ProblemDeviceInfo("Broken Device", @"ROOT\LEGACY", "System", 22, "Disabled", "Error")],
            NetworkAdapters = [new NetworkAdapterInfo("Intel Ethernet", "AA:BB:CC:DD:EE:FF", true, DriverVersion: "1.2.3", DriverDate: "2024-01-01")],
            WifiInterfaces = [new WifiInterfaceInfo("Wi-Fi", "connected", "MyNetwork", "aa:bb:cc:dd:ee:ff", "802.11ax", "WPA2-Personal", 36, 90, 866, 866, "Auto Connect")],
        },
        new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

    [Fact]
    public void ExportTxt_ContainsNewInventoryDetails()
    {
        var txt = new SnapshotExporter().ExportTxt(MakeSnapshot());

        Assert.Contains("C:", txt);
        Assert.Contains("内存拓扑", txt);
        Assert.Contains("Secure Boot", txt);
        Assert.Contains("TPM", txt);
        Assert.Contains("SN123", txt);
        Assert.Contains("ThinkPad", txt);
        Assert.Contains("Broken Device", txt);
        Assert.Contains("1.2.3", txt);
        Assert.Contains("Wi-Fi", txt);
        Assert.Contains("MyNetwork", txt);
    }

    [Fact]
    public void ExportJson_ContainsNewInventoryFields()
    {
        var json = new SnapshotExporter().ExportJson(MakeSnapshot());
        using var doc = JsonDocument.Parse(json);
        var inv = doc.RootElement.GetProperty("Inventory");

        Assert.Equal("C:", inv.GetProperty("LogicalDisks")[0].GetProperty("DeviceId").GetString());
        Assert.Equal(4, inv.GetProperty("MemoryTopology").GetProperty("TotalSlots").GetInt32());
        Assert.True(inv.GetProperty("SystemDetails").GetProperty("SecureBoot").GetBoolean());
        Assert.Equal("SN123", inv.GetProperty("SystemDetails").GetProperty("SerialNumber").GetString());
        Assert.Equal(2, inv.GetProperty("SystemDetails").GetProperty("VirtualizationBasedSecurityStatus").GetInt32());
        Assert.True(inv.GetProperty("SystemDetails").GetProperty("MemoryIntegrityEnabled").GetBoolean());
        Assert.Equal("Broken Device", inv.GetProperty("ProblemDevices")[0].GetProperty("Name").GetString());
        Assert.Equal("1.2.3", inv.GetProperty("NetworkAdapters")[0].GetProperty("DriverVersion").GetString());
        Assert.Equal("MyNetwork", inv.GetProperty("WifiInterfaces")[0].GetProperty("Ssid").GetString());
    }

    [Fact]
    public void ExportTxt_DriveTypesAreLocalized()
    {
        var snap = new Snapshot(
            DateTimeOffset.UtcNow,
            new HardwareInventory
            {
                LogicalDisks =
                [
                    new LogicalDiskInfo("A:", null, null, null, null, 2),
                    new LogicalDiskInfo("C:", null, null, null, null, 3),
                    new LogicalDiskInfo("Z:", null, null, null, null, 4),
                    new LogicalDiskInfo("D:", null, null, null, null, 5),
                    new LogicalDiskInfo("R:", null, null, null, null, 6),
                ],
            },
            new LiveMetrics { Timestamp = DateTimeOffset.UtcNow });

        var txt = new SnapshotExporter().ExportTxt(snap);

        Assert.Contains("可移动 Removable", txt);
        Assert.Contains("本地磁盘 Fixed", txt);
        Assert.Contains("网络 Network", txt);
        Assert.Contains("光盘 Optical", txt);
        Assert.Contains("内存盘 RAM Disk", txt);
    }
}
