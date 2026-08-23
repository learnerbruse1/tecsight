using TecSight.Core;

namespace TecSight.Core.Tests;

public class WlanInfoProviderTests
{
    [Fact]
    public void Parse_EnglishOutput_ExtractsConnectionDetails()
    {
        const string output = """
There is 1 interface on the system:

    Name                   : Wi-Fi
    Description            : Intel(R) Wi-Fi 6 AX201 160MHz
    GUID                   : 00000000-0000-0000-0000-000000000000
    Physical address       : 00:11:22:33:44:55
    State                  : connected
    SSID                   : MyNetwork
    BSSID                  : aa:bb:cc:dd:ee:ff
    Network type           : Infrastructure
    Radio type             : 802.11ax
    Authentication         : WPA2-Personal
    Cipher                 : CCMP
    Connection mode        : Auto Connect
    Channel                : 36
    Receive rate (Mbps)    : 866
    Transmit rate (Mbps)   : 866
    Signal                 : 90%
    Profile                : MyNetwork
""";

        var result = WlanInfoProvider.Parse(output);

        var w = Assert.Single(result);
        Assert.Equal("Wi-Fi", w.Name);
        Assert.Equal("connected", w.State);
        Assert.Equal("MyNetwork", w.Ssid);
        Assert.Equal("aa:bb:cc:dd:ee:ff", w.Bssid);
        Assert.Equal("802.11ax", w.RadioType);
        Assert.Equal("WPA2-Personal", w.Authentication);
        Assert.Equal("Auto Connect", w.ConnectionMode);
        Assert.Equal(36, w.Channel);
        Assert.Equal(866, w.ReceiveRateMbps);
        Assert.Equal(866, w.TransmitRateMbps);
        Assert.Equal(90, w.SignalPercent);
    }

    [Fact]
    public void Parse_ChineseOutput_ExtractsConnectionDetails()
    {
        const string output = """
系统上有 1 个接口：

    名称                   : WLAN
    描述                   : Intel(R) Wi-Fi 6 AX201 160MHz
    物理地址               : 00:11:22:33:44:55
    状态                   : 已连接
    SSID                   : 我的网络
    BSSID                  : aa:bb:cc:dd:ee:ff
    网络类型               : 基础结构
    无线电类型             : 802.11ax
    身份验证               : WPA2-个人
    密码                   : CCMP
    连接模式               : 自动连接
    信道                   : 36
    接收速率(Mbps)         : 866
    传输速率(Mbps)         : 866
    信号                   : 90%
""";

        var result = WlanInfoProvider.Parse(output);

        var w = Assert.Single(result);
        Assert.Equal("WLAN", w.Name);
        Assert.Equal("已连接", w.State);
        Assert.Equal("我的网络", w.Ssid);
        Assert.Equal("802.11ax", w.RadioType);
        Assert.Equal("WPA2-个人", w.Authentication);
        Assert.Equal("自动连接", w.ConnectionMode);
        Assert.Equal(36, w.Channel);
        Assert.Equal(866, w.ReceiveRateMbps);
        Assert.Equal(866, w.TransmitRateMbps);
        Assert.Equal(90, w.SignalPercent);
    }

    [Fact]
    public void Parse_NoWirelessInterface_ReturnsEmpty()
    {
        var result = WlanInfoProvider.Parse("系统上没有无线接口。\r\nThere is no wireless interface on the system.");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MultipleInterfaces_ReturnsAllInterfaces()
    {
        const string output = """
There are 2 interfaces on the system:

    Name                   : Wi-Fi
    State                  : connected
    SSID                   : Home

    Name                   : Ethernet
    State                  : connected
    SSID                   :
""";

        var result = WlanInfoProvider.Parse(output);

        Assert.Equal(2, result.Count);
        Assert.Equal("Wi-Fi", result[0].Name);
        Assert.Equal("Ethernet", result[1].Name);
    }

    [Fact]
    public void Parse_MalformedLines_AreIgnored()
    {
        var result = WlanInfoProvider.Parse("just some text\r\nAnother line without colon");

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_InvalidNumericFields_AreNull()
    {
        const string output = """
    Name                   : Wi-Fi
    Channel                : not-a-number
    Receive rate (Mbps)    : N/A
    Transmit rate (Mbps)   : not-a-number
    Signal                 : unknown
""";

        var w = Assert.Single(WlanInfoProvider.Parse(output));

        Assert.Null(w.Channel);
        Assert.Null(w.ReceiveRateMbps);
        Assert.Null(w.TransmitRateMbps);
        Assert.Null(w.SignalPercent);
    }
}
