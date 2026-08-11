using TecSight.Core;

namespace TecSight.Core.Tests;

public class FormatUtilLinkSpeedTests
{
    [Theory]
    [InlineData(long.MaxValue)]        // WMI 哨兵值：速率未知
    [InlineData(9_223_372_036_854_775_806L)] // 接近哨兵
    [InlineData(1_000_000_000_000L)]   // 1 Tbps 及以上视为异常
    public void LinkSpeed_ReturnsNullText_ForImplausibleValues(long bps)
    {
        Assert.Equal("N/A", FormatUtil.LinkSpeed(bps, "N/A"));
    }

    [Theory]
    [InlineData(1_000_000_000, "1.0 Gbps")]
    [InlineData(380_250_000, "380 Mbps")]
    [InlineData(100_000_000, "100 Mbps")]
    [InlineData(100_000_000_000, "100.0 Gbps")] // 真实 100Gbps 仍应显示
    [InlineData(1_500, "2 Kbps")]
    [InlineData(0, "N/A")]
    public void LinkSpeed_FormatsNormalValues(long bps, string expected)
    {
        Assert.Equal(expected, FormatUtil.LinkSpeed(bps, "N/A"));
    }

    [Fact]
    public void LinkSpeed_Null_ReturnsNullText()
    {
        Assert.Equal("—", FormatUtil.LinkSpeed(null, "—"));
    }
}
