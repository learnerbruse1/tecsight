using System.IO;
using System.Text.Json;

namespace TecSight.App;

/// <summary>应用设置持久化（%LOCALAPPDATA%\TecSight\settings.json）：记住语言与主题。</summary>
public static class AppSettings
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TecSight");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static string Language { get; private set; } = "";
    public static bool DarkTheme { get; private set; }

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var j = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
            if (j is null) return;
            Language = j.Language ?? "";
            DarkTheme = j.DarkTheme;
        }
        catch
        {
            // 设置损坏时忽略，用默认
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Settings
            {
                Language = Localization.LocalizationManager.Instance.CurrentLanguage,
                DarkTheme = Themes.ThemeManager.IsDark,
            }));
        }
        catch
        {
            // 保存失败不影响运行
        }
    }

    private sealed class Settings
    {
        public string? Language { get; set; }
        public bool DarkTheme { get; set; }
    }
}