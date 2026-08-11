using System.IO;
using System.Text.Json;

namespace TecSight.App;

/// <summary>应用设置持久化（%LOCALAPPDATA%\TecSight\settings.json）：语言/主题/窗口状态。</summary>
public static class AppSettings
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TecSight");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static string Language { get; private set; } = "";
    public static bool DarkTheme { get; private set; }
    public static double WindowLeft { get; private set; } = double.NaN;
    public static double WindowTop { get; private set; } = double.NaN;
    public static double WindowWidth { get; private set; } = 1100;
    public static double WindowHeight { get; private set; } = 700;
    public static bool WindowMaximized { get; private set; }

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var j = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
            if (j is null) return;
            Language = j.Language ?? "";
            DarkTheme = j.DarkTheme;
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

    /// <summary>保存语言与主题（不覆盖窗口状态）。</summary>
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Settings
            {
                Language = Localization.LocalizationManager.Instance.CurrentLanguage,
                DarkTheme = Themes.ThemeManager.IsDark,
                WindowLeft = WindowLeft,
                WindowTop = WindowTop,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
                WindowMaximized = WindowMaximized,
            }));
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
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }
    }
}