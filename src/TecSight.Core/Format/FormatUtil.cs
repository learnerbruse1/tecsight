using System.Globalization;

namespace TecSight.Core;

/// <summary>跨层共享的不变区域格式化（消除 Format/导出器/HTML 报告中的重复）。</summary>
public static class FormatUtil
{
    public static string Pct(double? v, string nullText) =>
        v is double x && double.IsFinite(x) ? x.ToString("0.0", CultureInfo.InvariantCulture) + "%" : nullText;

    public static string Number(double? v, string nullText) =>
        v is double x && double.IsFinite(x) ? x.ToString("0.##", CultureInfo.InvariantCulture) : nullText;

    public static string Bytes(double? b, string nullText) =>
        b is double x && double.IsFinite(x) ? HumanBytes(x) : nullText;

    public static string Bps(double? b, string nullText) =>
        b is double x && double.IsFinite(x) ? HumanBytes(x) + "/s" : nullText;

    public static string Gb(double? b, string nullText) =>
        b is double x && double.IsFinite(x) ? (x / 1073741824.0).ToString("0.0", CultureInfo.InvariantCulture) + " GB" : nullText;

    public static string Gb(long? b, string nullText) => b.HasValue ? Gb((double?)b.Value, nullText) : nullText;

    public static string Wh(double? v, string nullText) =>
        v is double x && double.IsFinite(x) ? x.ToString("0.0", CultureInfo.InvariantCulture) + " Wh" : nullText;

    public static string FreqMhz(double? v, string nullText) =>
        v is double x && double.IsFinite(x) ? x.ToString("0", CultureInfo.InvariantCulture) + " MHz" : nullText;

    public static string LinkSpeed(long? bps, string nullText) => bps switch
    {
        // WMI 对"速率未知"可能返回哨兵值（如 long.MaxValue），以及 >1Tbps 的异常值，一律按不可用处理
        >= 1_000_000_000_000 => nullText,
        >= 1_000_000_000 => (bps.Value / 1_000_000_000.0).ToString("0.0", CultureInfo.InvariantCulture) + " Gbps",
        >= 1_000_000 => (bps.Value / 1_000_000.0).ToString("0", CultureInfo.InvariantCulture) + " Mbps",
        >= 1000 => (bps.Value / 1000.0).ToString("0", CultureInfo.InvariantCulture) + " Kbps",
        _ => nullText,
    };

    private static string HumanBytes(double b)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var i = 0;
        while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
        return b.ToString("0.##", CultureInfo.InvariantCulture) + " " + units[i];
    }
}