using System.Globalization;

namespace TecSight.App;

/// <summary>数值格式化辅助（委托 Core.FormatUtil，跨层一致；使用不变区域设置）。</summary>
public static class Format
{
    public static string Pct(double? v) => Core.FormatUtil.Pct(v, "—");
    public static string Bytes(double? b) => Core.FormatUtil.Bytes(b, "—");
    public static string Bps(double? b) => Core.FormatUtil.Bps(b, "—");
    public static string Number(double? v) => Core.FormatUtil.Number(v, "—");
    public static string FreqMhz(double? mhz) => Core.FormatUtil.FreqMhz(mhz, "—");
    public static string LinkSpeed(long? bps) => Core.FormatUtil.LinkSpeed(bps, "—");
    public static string FreqGhz(double? mhz) =>
        mhz is double x && double.IsFinite(x) ? (x / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " GHz" : "—";

    /// <summary>运行时长（秒 → 天/小时/分，按语言）。</summary>
    public static string Uptime(double? seconds, string lang)
    {
        if (!seconds.HasValue) return "—";
        var t = TimeSpan.FromSeconds(seconds.Value);
        var zh = lang == "zh";
        if (t.TotalDays >= 1) return zh ? $"{t.Days} 天 {t.Hours} 小时" : $"{t.Days}d {t.Hours}h";
        if (t.TotalHours >= 1) return zh ? $"{t.Hours} 小时 {t.Minutes} 分" : $"{t.Hours}h {t.Minutes}m";
        if (t.TotalMinutes >= 1) return zh ? $"{Math.Max(1, t.Minutes)} 分" : $"{Math.Max(1, t.Minutes)}m";
        // 不足 1 分钟：按秒显示，避免把 30 秒显示成"1 分"
        return zh ? $"{Math.Max(1, (int)t.TotalSeconds)} 秒" : $"{Math.Max(1, (int)t.TotalSeconds)}s";
    }
}