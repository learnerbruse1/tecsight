using TecSight.Core;

namespace TecSight.Core.Tests;

public class PeripheralProbeVidPidTests
{
    [Theory]
    [InlineData(@"USB\VID_1234&PID_5678\ABC", "1234", "5678")]
    [InlineData(@"USB\VID_046D&PID_C52B\5&2B1C", "046D", "C52B")]
    [InlineData(@"PCI\VEN_8086&DEV_1234", null, null)]
    [InlineData(null, null, null)]
    public void ParseUsbVidPid_ExtractsVendorAndProduct(string? pnpId, string? vid, string? pid)
    {
        var (v, p) = PeripheralProbe.ParseUsbVidPid(pnpId);

        Assert.Equal(vid, v);
        Assert.Equal(pid, p);
    }
}
