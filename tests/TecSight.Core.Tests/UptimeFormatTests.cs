using TecSight.App;

namespace TecSight.Core.Tests;

public class UptimeFormatTests
{
    [Theory]
    [InlineData(30.0, "zh", "30 秒")]
    [InlineData(30.0, "en", "30s")]
    [InlineData(90.0, "zh", "1 分")]
    [InlineData(90.0, "en", "1m")]
    [InlineData(3661.0, "zh", "1 小时 1 分")]
    [InlineData(90061.0, "en", "1d 1h")]
    [InlineData(null, "zh", "—")]
    public void Uptime_FormatsAccurately(double? seconds, string lang, string expected)
    {
        Assert.Equal(expected, Format.Uptime(seconds, lang));
    }
}
