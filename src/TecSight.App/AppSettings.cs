using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TecSight.App;

/// <summary>应用设置持久化（%LOCALAPPDATA%\TecSight\settings.json）：语言/主题/窗口状态。</summary>
public static class AppSettings
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TecSight");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

    public static string Language { get; private set; } = "";
    public static bool DarkTheme { get; private set; }
    public static bool HideNetworkNoise { get; private set; }
    public static double RefreshIntervalSeconds { get; private set; } = 1;
    public static double PeripheralScanSeconds { get; private set; } = 10;
    public static double InventoryRefreshSeconds { get; private set; } = 60;
    public static int LastPage { get; private set; } // 上次浏览的页面（0 = 概览）
    public static double WindowLeft { get; private set; } = double.NaN;
    public static double WindowTop { get; private set; } = double.NaN;
    public static double WindowWidth { get; private set; } = 1100;
    public static double WindowHeight { get; private set; } = 700;
    public static bool WindowMaximized { get; private set; }

    public static void Load()
    {
        try
        {
            var tmpPath = FilePath + ".tmp";
            try
            {
                if (!File.Exists(FilePath) && File.Exists(tmpPath))
                {
                    File.Move(tmpPath, FilePath);
                }
                else if (File.Exists(FilePath) && File.Exists(tmpPath))
                {
                    File.Delete(tmpPath);
                }
            }
            catch
            {
                // 临时文件清理失败不应阻止读取已存在的主设置文件
            }
            if (!File.Exists(FilePath)) return;
            var j = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions);
            if (j is null) return;
            Language = j.Language is "zh" or "en" ? j.Language : "";
            DarkTheme = j.DarkTheme;
            HideNetworkNoise = j.HideNetworkNoise;
            RefreshIntervalSeconds = j.RefreshIntervalSeconds is >= 1 and <= 60 ? j.RefreshIntervalSeconds : 1;
            PeripheralScanSeconds = j.PeripheralScanSeconds is >= 5 and <= 300 ? j.PeripheralScanSeconds : 10;
            InventoryRefreshSeconds = j.InventoryRefreshSeconds is >= 30 and <= 600 ? j.InventoryRefreshSeconds : 60;
            LastPage = Enum.IsDefined(typeof(AppPage), j.LastPage) ? j.LastPage : 0;
            WindowLeft = j.WindowLeft;
            WindowTop = j.WindowTop;
            WindowWidth = j.WindowWidth > 0 ? j.WindowWidth : 1100;
            WindowHeight = j.WindowHeight > 0 ? j.WindowHeight : 700;
            WindowMaximized = j.WindowMaximized;
        }
        catch
        {
            // 设置损坏时忽略，用默认
        }
    }

    /// <summary>更新「隐藏网络过滤器噪音」偏好（下次 Save 时一并持久化）。</summary>
    public static void SetHideNetworkNoise(bool value) => HideNetworkNoise = value;

    public static void SetRefreshIntervalSeconds(double value) => RefreshIntervalSeconds = value;

    public static void SetPeripheralScanSeconds(double value) => PeripheralScanSeconds = value;

    public static void SetInventoryRefreshSeconds(double value) => InventoryRefreshSeconds = value;

    public static void SetDarkTheme(bool value) => DarkTheme = value;

    public static void SetLanguage(string value) => Language = value;

    /// <summary>更新上次浏览的页面（下次 Save 时持久化）。</summary>
    public static void SetLastPage(int value) => LastPage = value;

    /// <summary>保存语言与主题（不覆盖窗口状态）。</summary>
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(new Settings
            {
                Language = Language,
                DarkTheme = DarkTheme,
                HideNetworkNoise = HideNetworkNoise,
                RefreshIntervalSeconds = RefreshIntervalSeconds,
                PeripheralScanSeconds = PeripheralScanSeconds,
                InventoryRefreshSeconds = InventoryRefreshSeconds,
                LastPage = LastPage,
                WindowLeft = WindowLeft,
                WindowTop = WindowTop,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
                WindowMaximized = WindowMaximized,
            }, JsonOptions));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch
        {
            // 保存失败不影响运行
        }
    }

    /// <summary>保存窗口状态（窗口关闭时调用）。</summary>
    public static void SaveWindow(double left, double top, double width, double height, bool maximized)
    {
        WindowLeft = left;
        WindowTop = top;
        WindowWidth = width;
        WindowHeight = height;
        WindowMaximized = maximized;
        Save();
    }

    private sealed class Settings
    {
        public string? Language { get; set; }
        public bool DarkTheme { get; set; }
        public bool HideNetworkNoise { get; set; }
        public double RefreshIntervalSeconds { get; set; } = 1;
        public double PeripheralScanSeconds { get; set; } = 10;
        public double InventoryRefreshSeconds { get; set; } = 60;
        public int LastPage { get; set; }
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }
    }
}
