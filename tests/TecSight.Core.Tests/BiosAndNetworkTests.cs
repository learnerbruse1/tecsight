using System.Text.Json;
using TecSight.Core;
using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class BiosAndNetworkExportTests
{
    private static Snapshot MakeSnapshot() => new(
        new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        new HardwareInventory
        {
            ComputerName = "FAKE-PC",
            Bios = new BiosInfo("AMI", "UEFI", "1.2.3", "3.4", "2024-01-02", "SN-BIOS",
                Description: "System BIOS",
                BuildNumber: "20240102",
                IdentificationCode: "ID-1",
                LanguageEdition: "en|US|iso8859-1",
                SystemBiosMajorVersion: 1,
                SystemBiosMinorVersion: 2,
                EmbeddedControllerMajorVersion: 3,
                EmbeddedControllerMinorVersion: 4,
                PrimaryBios: true,
                Status: "OK"),
            NetworkAdapters =
            [
                new NetworkAdapterInfo("Intel Ethernet", "AA:BB:CC:DD:EE:FF", true, 1_000_000_000, "Ethernet 802.3",
                    Manufacturer: "Intel",
                    PnpDeviceId: @"PCI\VEN_8086",
                    NetConnectionStatus: 2,
                    Index: 1,
                    NetConnectionId: "Ethernet"),
            ],
            NetworkConfigurations =
            [
                new NetworkConfigInfo("Intel Ethernet", ["192.168.1.10"], ["192.168.1.1"], ["1.1.1.1"],
                    Index: 1,
                    DhcpEnabled: false),
            ],
        },
        new LiveMetrics { Timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero) });

    [Fact]
    public void ExportTxt_ContainsBiosAndNetworkDetails()
    {
        var txt = new SnapshotExporter().ExportTxt(MakeSnapshot());

        Assert.Contains("AMI", txt);
        Assert.Contains("SN-BIOS", txt);
        Assert.Contains("Intel Ethernet", txt);
        Assert.Contains("Intel", txt);
    }

    [Fact]
    public void ExportHtml_ContainsBiosSection()
    {
        var html = new SnapshotExporter().ExportHtml(MakeSnapshot());

        Assert.Contains("BIOS", html);
        Assert.Contains("AMI", html);
        Assert.Contains("SN-BIOS", html);
        Assert.Contains("Intel Ethernet", html);
    }

    [Fact]
    public void ExportJson_ContainsBiosAndNetworkFields()
    {
        var json = new SnapshotExporter().ExportJson(MakeSnapshot());
        using var doc = JsonDocument.Parse(json);
        var inv = doc.RootElement.GetProperty("Inventory");

        Assert.Equal("AMI", inv.GetProperty("Bios").GetProperty("Manufacturer").GetString());
        Assert.Equal("SN-BIOS", inv.GetProperty("Bios").GetProperty("SerialNumber").GetString());

        var net = inv.GetProperty("NetworkAdapters")[0];
        Assert.Equal("Intel Ethernet", net.GetProperty("Name").GetString());
        Assert.Equal("Intel", net.GetProperty("Manufacturer").GetString());
        Assert.Equal(2, net.GetProperty("NetConnectionStatus").GetInt32());

        var cfg = inv.GetProperty("NetworkConfigurations")[0];
        Assert.Equal(1, cfg.GetProperty("Index").GetInt32());
        Assert.False(cfg.GetProperty("DhcpEnabled").GetBoolean());
    }
}
