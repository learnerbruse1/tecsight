using TecSight.Core;

using TecSight.Core.Models;

namespace TecSight.Core.Tests;

public class PeripheralClassifyTests
{
    [Theory]
    [InlineData("Keyboard", "HID Keyboard Device", null, "keyboard")]
    [InlineData("Mouse", "HID-compliant mouse", null, "mouse")]
    [InlineData("Camera", "Integrated Camera", null, "camera")]
    [InlineData("Monitor", "Generic PnP Monitor", null, "display")]
    [InlineData("PrintQueue", "Microsoft Print to PDF", null, "printer")]
    [InlineData("MEDIA", "Speakers", null, "audio")]
    [InlineData("AudioEndpoint", "Microphone", null, "audio")]
    [InlineData("Bluetooth", "Bluetooth Radio", null, "bluetooth")]
    public void Classify_ByPnpClass(string pnpClass, string name, string? desc, string expected)
    {
        Assert.Equal(expected, PeripheralProbe.Classify(pnpClass, name, desc));
    }

    [Theory]
    [InlineData("USB", "USB Root Hub", null, "hub")]
    [InlineData("USB", "Generic Card Reader", null, "cardreader")]
    [InlineData("USB", "Android Phone (MTP)", null, "phone")]
    [InlineData("USB", "USB Mass Storage Device", null, "storage")]
    [InlineData("USB", "USB Audio Device", null, "audio")]
    [InlineData("USB", "USB Camera", null, "camera")]
    [InlineData("USB", "USB Video Device", null, "camera")]
    [InlineData("USB", "USB Bluetooth Adapter", null, "bluetooth")]
    [InlineData("USB", "USB Input Device", null, "input")]
    [InlineData("USB", "USB Network Adapter", null, "network")]
    [InlineData("USB", "Generic USB Device", null, "usb")]
    [InlineData("USBHub", "Generic USB Hub", null, "hub")]
    public void Classify_UsbByDescription(string pnpClass, string name, string? desc, string expected)
    {
        Assert.Equal(expected, PeripheralProbe.Classify(pnpClass, name, desc));
    }

    [Theory]
    [InlineData("HIDClass", "HID Keyboard Device", null, "keyboard")]
    [InlineData("HIDClass", "HID-compliant mouse", null, "mouse")]
    [InlineData("HIDClass", "Xbox Game Controller", null, "gamepad")]
    [InlineData("HIDClass", "Integrated Camera", null, "camera")]
    [InlineData("HIDClass", "Generic HID Device", null, "input")]
    public void Classify_HidByDescription(string pnpClass, string name, string? desc, string expected)
    {
        Assert.Equal(expected, PeripheralProbe.Classify(pnpClass, name, desc));
    }

    [Fact]
    public void Classify_UnknownFallsBackToOther()
    {
        Assert.Equal("other", PeripheralProbe.Classify("SomethingElse", "Mystery Device", null));
    }

    [Fact]
    public void Classify_CameraInDefaultBranch()
    {
        Assert.Equal("camera", PeripheralProbe.Classify("Image", "Integrated Camera", null));
    }

    [Fact]
    public void FromInventory_PreservesPnpIdsAndManufacturers()
    {
        var inv = new HardwareInventory
        {
            AudioDevices = [new AudioDeviceInfo("Speakers", "Vendor", "OK", @"HDAUDIO\FUNC_01")],
            Keyboards = [new PnPDeviceInfo("HID Keyboard Device", "Keyboard", "OK", @"USB\VID_1234&PID_5678", "Vendor")],
            PointingDevices = [new PnPDeviceInfo("HID-compliant mouse", "Mouse", "OK", @"USB\VID_ABCD&PID_EF01", "Vendor")],
        };

        var result = PeripheralProbe.FromInventory(inv);

        var audio = Assert.Single(result, d => d.Category == "audio");
        Assert.Equal(@"HDAUDIO\FUNC_01", audio.PnpDeviceId);
        Assert.Equal("Vendor", audio.Manufacturer);

        var keyboard = Assert.Single(result, d => d.Category == "keyboard");
        Assert.Equal(@"USB\VID_1234&PID_5678", keyboard.PnpDeviceId);
        Assert.Equal("Vendor", keyboard.Manufacturer);

        var mouse = Assert.Single(result, d => d.Category == "mouse");
        Assert.Equal(@"USB\VID_ABCD&PID_EF01", mouse.PnpDeviceId);
        Assert.Equal("Vendor", mouse.Manufacturer);
    }

    [Fact]
    public void FromInventory_AddsPhysicalNetworkAdaptersAsPeripherals()
    {
        var inv = new HardwareInventory
        {
            NetworkAdapters =
            [
                new NetworkAdapterInfo(
                    "Intel Ethernet I219-V",
                    "AA:BB:CC:DD:EE:FF",
                    IsPhysical: true,
                    SpeedBps: 1_000_000_000,
                    AdapterType: "Ethernet 802.3",
                    Manufacturer: "Intel",
                    PnpDeviceId: @"PCI\VEN_8086"),
                new NetworkAdapterInfo(
                    "Virtual TAP Adapter",
                    null,
                    IsPhysical: false,
                    AdapterType: "TAP",
                    PnpDeviceId: @"ROOT\NET\0000"),
            ],
        };

        var result = PeripheralProbe.FromInventory(inv);

        var network = Assert.Single(result, d => d.Category == "network");
        Assert.Contains("Intel Ethernet", network.Name);
        Assert.DoesNotContain(result, d => d.Name?.Contains("Virtual TAP", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void FromInventory_ClassifiesUsbDevicesByDescription()
    {
        var inv = new HardwareInventory
        {
            UsbDevices =
            [
                new UsbDeviceInfo("USB Audio Device", "Vendor", "OK", @"USB\VID_1234&PID_5678"),
                new UsbDeviceInfo("USB Mass Storage Device", "Vendor", "OK", @"USB\VID_ABCD&PID_EF01"),
            ],
        };

        var result = PeripheralProbe.FromInventory(inv);

        Assert.Single(result, d => d.Category == "audio" && d.PnpDeviceId == @"USB\VID_1234&PID_5678");
        Assert.Single(result, d => d.Category == "storage" && d.PnpDeviceId == @"USB\VID_ABCD&PID_EF01");
    }
}
