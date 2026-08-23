using System.Diagnostics;
using System.Globalization;
using System.Text;
using ManagedNativeWifi;
using TecSight.Core.Models;

namespace TecSight.Core;

/// <summary>
/// Wi-Fi 接口详情数据源：优先使用 ManagedNativeWifi 原生 WLAN API；
/// 不可用时回退到 netsh wlan show interfaces 并解析输出。
/// </summary>
public static class WlanInfoProvider
{
    public static IReadOnlyList<WifiInterfaceInfo> Scan()
    {
        try
        {
            return ScanWithNativeWifi();
        }
        catch
        {
            return ScanWithNetsh();
        }
    }

    private static IReadOnlyList<WifiInterfaceInfo> ScanWithNativeWifi()
    {
        var channels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var bss in NativeWifi.EnumerateBssNetworks())
            {
                var bssid = FormatNetworkIdentifier(bss.Bssid);
                if (!string.IsNullOrWhiteSpace(bssid))
                {
                    channels[bssid] = bss.Channel;
                }
            }
        }
        catch
        {
            // 信道信息失败时仍可返回连接基本信息
        }

        var result = new List<WifiInterfaceInfo>();
        foreach (var iface in NativeWifi.EnumerateInterfaces())
        {
            var state = InterfaceStateText(iface.State);
            var (action, conn) = NativeWifi.GetCurrentConnection(iface.Id);
            if (action == ActionResult.Success)
            {
                var bssid = FormatNetworkIdentifier(conn.Bssid);
                channels.TryGetValue(bssid ?? "", out var channel);
                result.Add(new WifiInterfaceInfo(
                    iface.Description,
                    state,
                    conn.Ssid.ToString(),
                    bssid,
                    conn.PhyType.ToProtocolName(),
                    AuthenticationText(conn.AuthenticationAlgorithm),
                    channel > 0 ? channel : null,
                    conn.SignalQuality,
                    conn.RxRate > 0 ? conn.RxRate / 1000.0 : null,
                    conn.TxRate > 0 ? conn.TxRate / 1000.0 : null,
                    conn.ConnectionMode.ToString().ToLowerInvariant()));
            }
            else
            {
                result.Add(new WifiInterfaceInfo(
                    iface.Description,
                    state,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null));
            }
        }
        return result;
    }

    private static IReadOnlyList<WifiInterfaceInfo> ScanWithNetsh()
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

    private static string InterfaceStateText(InterfaceState state) => state switch
    {
        InterfaceState.Connected => "connected",
        InterfaceState.Disconnected => "disconnected",
        InterfaceState.NotReady => "not ready",
        InterfaceState.Disconnecting => "disconnecting",
        InterfaceState.Associating => "associating",
        InterfaceState.Discovering => "discovering",
        InterfaceState.Authenticating => "authenticating",
        InterfaceState.AdHocNetworkFormed => "ad hoc",
        _ => state.ToString().ToLowerInvariant(),
    };

    private static string? AuthenticationText(AuthenticationAlgorithm algorithm) => algorithm switch
    {
        AuthenticationAlgorithm.Unknown => null,
        AuthenticationAlgorithm.Open => "Open",
        AuthenticationAlgorithm.Shared => "Shared",
        AuthenticationAlgorithm.WPA => "WPA",
        AuthenticationAlgorithm.WPA_PSK => "WPA-PSK",
        AuthenticationAlgorithm.WPA_NONE => "WPA-None",
        AuthenticationAlgorithm.RSNA => "WPA2",
        AuthenticationAlgorithm.RSNA_PSK => "WPA2-PSK",
        AuthenticationAlgorithm.WPA3_ENT_192 => "WPA3-Enterprise 192",
        AuthenticationAlgorithm.WPA3_ENT => "WPA3-Enterprise",
        AuthenticationAlgorithm.WPA3_SAE => "WPA3-SAE",
        AuthenticationAlgorithm.OWE => "OWE",
        _ => algorithm.ToString(),
    };

    private static string? FormatNetworkIdentifier(NetworkIdentifier identifier)
    {
        try
        {
            var bytes = identifier.ToBytes();
            if (bytes.Length == 6)
            {
                return string.Join(":", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
            }
            return identifier.ToString();
        }
        catch
        {
            return null;
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
