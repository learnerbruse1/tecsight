using TecSight.Core;

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
    [InlineData("USB", "Generic USB Device", null, "usb")]
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
}
