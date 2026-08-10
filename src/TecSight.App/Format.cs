namespace TecSight.App;

/// <summary>数值格式化辅助。</summary>
public static class Format
{
    public static string Pct(double? v) => v.HasValue ? $"{v.Value:0.0}%" : "—";
    public static string Bytes(double? b) => b.HasValue ? HumanBytes(b.Value) : "—";
    public static string Bps(double? b) => b.HasValue ? HumanBytes(b.Value) + "/s" : "—";
    public static string Number(double? v) => v.HasValue ? v.Value.ToString("0.##") : "—";
    public static string FreqMhz(double? mhz) => mhz.HasValue ? $"{mhz.Value:0} MHz" : "—";
    public static string FreqGhz(double? mhz) => mhz.HasValue ? $"{mhz.Value / 1000.0:0.00} GHz" : "—";

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
        return $"{b:0.##} {units[i]}";
    }
}