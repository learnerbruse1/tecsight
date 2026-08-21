using System.Diagnostics;
using System.Globalization;
using System.Text;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>
/// Wi-Fi 接口详情数据源：调用 netsh wlan show interfaces 并解析输出。
/// 无法执行或解析失败时返回空列表（降级），不抛异常。
/// </summary>
public static class WlanInfoProvider
{
    public static IReadOnlyList<WifiInterfaceInfo> Scan()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var encoding = TryGetConsoleEncoding();
            if (encoding is not null)
            {
                psi.StandardOutputEncoding = encoding;
            }

            using var process = Process.Start(psi);
            if (process is null) return [];
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return Parse(output);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>解析 netsh wlan show interfaces 输出（中英文标签均兼容）。</summary>
    public static IReadOnlyList<WifiInterfaceInfo> Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return [];

        var result = new List<WifiInterfaceInfo>();
        WifiInterfaceInfo? current = null;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var idx = line.IndexOf(':');
            if (idx < 0) continue;
            var label = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();

            if (Match(label, "name", "名称"))
            {
                if (current is not null) result.Add(current);
                current = new WifiInterfaceInfo(Name: value, null, null, null, null, null, null, null, null, null, null);
                continue;
            }

            if (current is not null)
            {
                current = ApplyField(current, label, value);
            }
        }

        if (current is not null) result.Add(current);
        return result;
    }

    private static WifiInterfaceInfo ApplyField(WifiInterfaceInfo info, string label, string value)
    {
        // BSSID 必须放在 SSID 之前，避免 "bssid" 被 "ssid" 误匹配
        if (Match(label, "bssid", "BSSID")) return info with { Bssid = value };
        if (Match(label, "ssid", "SSID")) return info with { Ssid = value };
        if (Match(label, "state", "状态")) return info with { State = value };
        if (Match(label, "radiotype", "无线电类型")) return info with { RadioType = value };
        if (Match(label, "authentication", "身份验证")) return info with { Authentication = value };
        if (Match(label, "channel", "信道")) return info with { Channel = TryInt(value) };
        if (Match(label, "signal", "信号")) return info with { SignalPercent = TryPercent(value) };
        if (Match(label, "receiverate", "接收速率")) return info with { ReceiveRateMbps = TryDouble(value) };
        if (Match(label, "transmitrate", "传输速率")) return info with { TransmitRateMbps = TryDouble(value) };
        if (Match(label, "connectionmode", "连接模式")) return info with { ConnectionMode = value };
        return info;
    }

    private static bool Match(string label, string en, string zh)
    {
        var norm = label.ToLowerInvariant().Replace(" ", "");
        return norm.Contains(en.Replace(" ", "").ToLowerInvariant())
            || label.Contains(zh, StringComparison.Ordinal);
    }

    private static int? TryInt(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static double? TryDouble(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static double? TryPercent(string value)
    {
        var cleaned = value.Replace("%", "").Trim();
        return TryDouble(cleaned);
    }

    private static Encoding? TryGetConsoleEncoding()
    {
        try
        {
            return Encoding.GetEncoding(CultureInfo.InstalledUICulture.TextInfo.OEMCodePage);
        }
        catch
        {
            return null;
        }
    }
}
