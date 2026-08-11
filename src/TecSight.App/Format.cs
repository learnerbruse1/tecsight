using System.Globalization;

namespace TecSight.App;

/// <summary>数值格式化辅助（使用不变区域设置，避免不同语言环境小数点差异）。</summary>
public static class Format
{
    public static string Pct(double? v) => v is double x && double.IsFinite(x) ? x.ToString("0.0", CultureInfo.InvariantCulture) + "%" : "—";
    public static string Bytes(double? b) => b is double x && double.IsFinite(x) ? HumanBytes(x) : "—";
    public static string Bps(double? b) => b is double x && double.IsFinite(x) ? HumanBytes(x) + "/s" : "—";
    public static string Number(double? v) => v is double x && double.IsFinite(x) ? x.ToString("0.##", CultureInfo.InvariantCulture) : "—";
    public static string FreqMhz(double? mhz) => mhz is double x && double.IsFinite(x) ? x.ToString("0", CultureInfo.InvariantCulture) + " MHz" : "—";
    public static string FreqGhz(double? mhz) => mhz is double x && double.IsFinite(x) ? (x / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " GHz" : "—";
    public static string LinkSpeed(long? bps) => bps switch
    {
        >= 1_000_000_000 => (bps.Value / 1_000_000_000.0).ToString("0.0", CultureInfo.InvariantCulture) + " Gbps",
        >= 1_000_000 => (bps.Value / 1_000_000.0).ToString("0", CultureInfo.InvariantCulture) + " Mbps",
        >= 1000 => (bps.Value / 1000.0).ToString("0", CultureInfo.InvariantCulture) + " Kbps",
        _ => "—",
    };

    /// <summary>运行时长（秒 → 天/小时/分，按语言）。</summary>
    public static string Uptime(double? seconds, string lang)
    {
        if (!seconds.HasValue) return "—";
        var t = TimeSpan.FromSeconds(seconds.Value);
        var zh = lang == "zh";
        if (t.TotalDays >= 1) return zh ? $"{t.Days} 天 {t.Hours} 小时" : $"{t.Days}d {t.Hours}h";
        if (t.TotalHours >= 1) return zh ? $"{t.Hours} 小时 {t.Minutes} 分" : $"{t.Hours}h {t.Minutes}m";
        return zh ? $"{Math.Max(1, t.Minutes)} 分" : $"{Math.Max(1, t.Minutes)}m";
    }

    private static string HumanBytes(double b)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var i = 0;
        while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
        return b.ToString("0.##", CultureInfo.InvariantCulture) + " " + units[i];
    }
}