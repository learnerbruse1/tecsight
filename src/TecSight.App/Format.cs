namespace TecSight.App;

/// <summary>数值格式化辅助。</summary>
public static class Format
{
    public static string Pct(double? v) => v.HasValue ? $"{v.Value:0.0}%" : "—";
    public static string Bytes(double? b) => b.HasValue ? HumanBytes(b.Value) : "—";
    public static string Bps(double? b) => b.HasValue ? HumanBytes(b.Value) + "/s" : "—";
    public static string Number(double? v) => v.HasValue ? v.Value.ToString("0.##") : "—";

    private static string HumanBytes(double b)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var i = 0;
        while (b >= 1024 && i < units.Length - 1) { b /= 1024; i++; }
        return $"{b:0.##} {units[i]}";
    }
}